﻿namespace UglyToad.Pdf.Tokenization.Tokens
{
    using System;
    using System.Globalization;

    public class NumericToken : IDataToken<decimal>
    {
        public decimal Data { get; }

        public bool IsWhole { get; }

        public int Int { get; }

        public bool IsBiggerThanInt { get; }

        public long Long { get; }

        public NumericToken(decimal value)
        {
            Data = value;
            IsWhole = decimal.Floor(value) == value;
            Long = (long) value;

            try
            {
                Int = (int) value;
            }
            catch (OverflowException)
            {
                IsBiggerThanInt = true;
            }
        }

        public override string ToString()
        {
            return Data.ToString(NumberFormatInfo.InvariantInfo);
        }
    }
}