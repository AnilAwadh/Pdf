﻿namespace UglyToad.Pdf.Filters
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using ContentStream;
    using Cos;
    using Logging;

    internal class MemoryFilterProvider : IFilterProvider
    {
        private readonly IReadOnlyDictionary<CosName, Func<IFilter>> filterFactories; 

        public MemoryFilterProvider(IDecodeParameterResolver decodeParameterResolver, IPngPredictor pngPredictor, ILog log)
        {
            IFilter Ascii85Func() => new Ascii85Filter();
            IFilter AsciiHexFunc() => new AsciiHexDecodeFilter();
            IFilter FlateFunc() => new FlateFilter(decodeParameterResolver, pngPredictor, log);
            IFilter RunLengthFunc() => new RunLengthFilter();

            filterFactories = new Dictionary<CosName, Func<IFilter>>
            {
                {CosName.ASCII85_DECODE, Ascii85Func},
                {CosName.ASCII85_DECODE_ABBREVIATION, Ascii85Func},
                {CosName.ASCII_HEX_DECODE, AsciiHexFunc},
                {CosName.ASCII_HEX_DECODE_ABBREVIATION, AsciiHexFunc},
                {CosName.FLATE_DECODE, FlateFunc},
                {CosName.FLATE_DECODE_ABBREVIATION, FlateFunc},
                {CosName.RUN_LENGTH_DECODE, RunLengthFunc},
                {CosName.RUN_LENGTH_DECODE_ABBREVIATION, RunLengthFunc}
            };
        }

        public IReadOnlyList<IFilter> GetFilters(PdfDictionary streamDictionary)
        {
            if (streamDictionary == null)
            {
                throw new ArgumentNullException(nameof(streamDictionary));
            }

            var filterObject = streamDictionary.GetItemOrDefault(CosName.FILTER);

            if (filterObject == null)
            {
                return new IFilter[0];
            }

            switch (filterObject)
            {
                case COSArray filters:
                    // TODO: presumably this may be invalid...
                    return filters.Select(x => GetFilterStrict((CosName) x)).ToList();
                case CosName name:
                    return new[] {GetFilterStrict(name)};
                default:
                    throw new InvalidOperationException("The filter for a stream may be either a string or an array, instead this Pdf has: " 
                                                        + filterObject.GetType());
            }
        }

        private IFilter GetFilterStrict(CosName name)
        {
            if (!filterFactories.TryGetValue(name, out var factory))
            {
                throw new NotSupportedException($"The filter with the name {name} is not supported yet. Please raise an issue.");
            }

            return factory();
        }

        public IReadOnlyList<IFilter> GetAllFilters()
        {
            throw new System.NotImplementedException();
        }
    }
}