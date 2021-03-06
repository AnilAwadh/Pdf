﻿namespace UglyToad.Pdf.Content
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    public class Page
    {
        /// <summary>
        /// The page number (starting at 1).
        /// </summary>
        public int Number { get; }

        internal MediaBox MediaBox { get; }

        internal CropBox CropBox { get; }

        internal PageContent Content { get; }

        public IReadOnlyList<Letter> Letters => Content?.Letters ?? new Letter[0];

        public string Text { get; }

        /// <summary>
        /// Gets the width of the page in points.
        /// </summary>
        public decimal Width { get; }

        /// <summary>
        /// Gets the height of the page in points.
        /// </summary>
        public decimal Height { get; }

        /// <summary>
        /// The size of the page according to the standard page sizes or Custom if no matching standard size found.
        /// </summary>
        public PageSize Size { get; }

        internal Page(int number, MediaBox mediaBox, CropBox cropBox, PageContent content)
        {
            if (number <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(number), "Page number cannot be 0 or negative.");
            }

            Number = number;
            MediaBox = mediaBox;
            CropBox = cropBox;
            Content = content;
            Text = GetText(content);

            Width = mediaBox.Bounds.Width;
            Height = mediaBox.Bounds.Height;

            Size = mediaBox.Bounds.GetPageSize();
        }

        private static string GetText(PageContent content)
        {
            if (content?.Letters == null)
            {
                return string.Empty;
            }

            return string.Join(string.Empty, content.Letters.Select(x => x.Value));
        }
    }
}