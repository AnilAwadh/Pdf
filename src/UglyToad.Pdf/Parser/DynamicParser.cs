﻿namespace UglyToad.Pdf.Parser
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using ContentStream;
    using Cos;
    using IO;
    using Logging;
    using Parts;
    using Util;

    internal class DynamicParser
    {
        private readonly ILog log;
        private readonly CosBaseParser baseParser;
        private readonly CosStreamParser streamParser;
        private readonly ObjectStreamParser objectStreamParser;

        public DynamicParser(ILog log, CosBaseParser baseParser, CosStreamParser streamParser, ObjectStreamParser objectStreamParser)
        {
            this.log = log;
            this.baseParser = baseParser ?? throw new ArgumentNullException(nameof(baseParser));
            this.streamParser = streamParser ?? throw new ArgumentNullException(nameof(streamParser));
            this.objectStreamParser = objectStreamParser ?? throw new ArgumentNullException(nameof(objectStreamParser));
        }

        public CosBase Parse(ParsingArguments arguments, CosObject obj, bool requiresExistingObject)
        {
            return Parse(arguments.Reader, obj, arguments.CachingProviders.ObjectPool,
                arguments.CrossReferenceTable, arguments.CachingProviders.BruteForceSearcher,
                arguments.IsLenientParsing,
                requiresExistingObject);
        }

        public CosBase Parse(ParsingArguments arguments, CosObjectKey key, bool requiresExistingObject)
        {
            return Parse(arguments.Reader, key.Number, (int)key.Generation, arguments.CachingProviders.ObjectPool,
                arguments.CrossReferenceTable, arguments.CachingProviders.BruteForceSearcher,
                arguments.IsLenientParsing, requiresExistingObject);
        }

        public CosBase Parse(IRandomAccessRead reader, CosObject obj, CosObjectPool pool,
            CrossReferenceTable crossReferenceTable, BruteForceSearcher bruteForceSearcher, bool isLenient, bool requireExistingObject)
        {
            if (obj == null)
            {
                throw new ArgumentNullException(nameof(obj));
            }

            return Parse(reader, obj.GetObjectNumber(), obj.GetGenerationNumber(), pool,
                crossReferenceTable, bruteForceSearcher, isLenient, requireExistingObject);
        }

        public CosBase Parse(IRandomAccessRead reader, long objectNumber, int objectGeneration,
            CosObjectPool pool, CrossReferenceTable crossReferenceTable, 
            BruteForceSearcher bruteForceSearcher,
            bool isLenient,
            bool requireExistingObject)
        {
            if (pool == null)
            {
                throw new ArgumentNullException(nameof(pool));
            }

            var key = new CosObjectKey(objectNumber, objectGeneration);

            var pdfObject = pool.GetOrCreateDefault(key);

            if (pdfObject.GetObject() != null)
            {
                return pdfObject.GetObject();
            }

            if (crossReferenceTable == null)
            {
                throw new ArgumentNullException(nameof(crossReferenceTable));
            }

            var offsetOrStreamNumber = TryGet(key, crossReferenceTable.ObjectOffsets);

            if (requireExistingObject && (offsetOrStreamNumber == null || offsetOrStreamNumber <= 0))
            {
                throw new InvalidOperationException("Object must be defined and not compressed: " + key);
            }

            if (isLenient && offsetOrStreamNumber == null)
            {
                var locations = bruteForceSearcher.GetObjectLocations();

                offsetOrStreamNumber = TryGet(key, locations);

                if (offsetOrStreamNumber != null)
                {
                    crossReferenceTable.UpdateOffset(key, offsetOrStreamNumber.Value);
                }
            }

            if (offsetOrStreamNumber == null)
            {
                return CosNull.Null;
            }

            var isCompressedStreamObject = offsetOrStreamNumber <= 0;

            if (!isCompressedStreamObject)
            {
                return ParseObjectFromFile(offsetOrStreamNumber.Value, reader, key, pool, isLenient);
            }

            return ParseCompressedStreamObject(reader, -offsetOrStreamNumber.Value, objectNumber, pool, crossReferenceTable, bruteForceSearcher, isLenient);
        }
        
        private CosBase ParseObjectFromFile(long offset, IRandomAccessRead reader,
            CosObjectKey key,
            CosObjectPool pool,
            bool isLenientParsing)
        {
            reader.Seek(offset);

            var objectNumber = ObjectHelper.ReadObjectNumber(reader);
            var objectGeneration = ObjectHelper.ReadGenerationNumber(reader);

            ReadHelper.ReadExpectedString(reader, "obj", true);

            if (objectNumber != key.Number || objectGeneration != key.Generation)
            {
                throw new InvalidOperationException($"Xref for {key} points to object {objectNumber} {objectGeneration} at {offset}");
            }

            ReadHelper.SkipSpaces(reader);

            var baseObject = baseParser.Parse(reader, pool);

            var endObjectKey = ReadHelper.ReadString(reader);

            var atStreamStart = string.Equals(endObjectKey, "stream");

            if (atStreamStart)
            {
                var streamStartBytes = OtherEncodings.StringAsLatin1Bytes(endObjectKey);

                reader.Rewind(streamStartBytes.Length);

                baseObject = ReadNormalObjectStream(reader, baseObject, offset, isLenientParsing, out endObjectKey);
            }

            var message =
                $"Object ({objectNumber}:{objectGeneration}) at offset {offset} does not end with \'endobj\' but with \'{endObjectKey}\'";

            if (isLenientParsing)
            {
                log.Warn(message);
            }
            else
            {
                throw new InvalidOperationException(message);
            }

            return baseObject;
        }

        private CosBase ReadNormalObjectStream(IRandomAccessRead reader, CosBase currentBase, long offset,
            bool isLenientParsing,
            out string endObjectKey)
        {
            if (currentBase is PdfDictionary dictionary)
            {
                PdfRawStream stream = streamParser.Parse(reader, dictionary, isLenientParsing, null);

                currentBase = stream;
            }
            else
            {
                // this is not legal
                // the combination of a dict and the stream/endstream
                // forms a complete stream object
                throw new InvalidOperationException($"Stream not preceded by dictionary (offset: {offset}).");
            }

            ReadHelper.SkipSpaces(reader);
            endObjectKey = ReadHelper.ReadLine(reader);

            // we have case with a second 'endstream' before endobj
            if (!endObjectKey.StartsWith("endobj") && endObjectKey.StartsWith("endstream"))
            {
                endObjectKey = endObjectKey.Substring(9).Trim();
                if (endObjectKey.Length == 0)
                {
                    // no other characters in extra endstream line
                    // read next line
                    endObjectKey = ReadHelper.ReadLine(reader);
                }
            }

            return currentBase;
        }

        private CosBase ParseCompressedStreamObject(IRandomAccessRead reader, long streamObjectNumber, long requestedNumber, CosObjectPool objectPool, CrossReferenceTable crossReferenceTable, BruteForceSearcher bruteForceSearcher, bool isLenientParsing)
        {
            var baseStream = Parse(reader, streamObjectNumber, 0, objectPool, crossReferenceTable, bruteForceSearcher,
                isLenientParsing, true);

            if (!(baseStream is PdfRawStream stream))
            {
                log.Warn($"Could not find a stream for the object number, defaults to returning CosNull: {streamObjectNumber}");

                return CosNull.Null;
            }

            var objects = objectStreamParser.Parse(stream, objectPool);

            // register all objects which are referenced to be contained in object stream
            foreach (var next in objects)
            {
                var streamKey = new CosObjectKey(next);
                var offset = TryGet(streamKey, crossReferenceTable.ObjectOffsets);

                if (offset != null && offset == -streamObjectNumber)
                {
                    var streamObject = objectPool.Get(streamKey);
                    streamObject.SetObject(next.GetObject());
                }
            }

            var matchingStreamObject = objects.FirstOrDefault(x => x.GetObjectNumber() == requestedNumber);

            if (matchingStreamObject != null)
            {
                return matchingStreamObject;
            }

            log.Error($"Could not find the object {requestedNumber} in the stream for object {streamObjectNumber}. Returning CosNull.");

            return CosNull.Null;
        }

        private static T? TryGet<T, TKey>(TKey key, IReadOnlyDictionary<TKey, T> dictionary) where T : struct
        {
            return dictionary.TryGetValue(key, out var value) ? value : default(T?);
        }
    }
}
