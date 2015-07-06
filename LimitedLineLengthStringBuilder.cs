using System.Text;
using System;

namespace WowClientDB2MySQLTableGenerator
{
    public sealed class LimitedLineLengthStringBuilder
    {
        private StringBuilder _builder;
        private StringBuilder _lineBuffer;
        public int Length
        {
            get { return _builder.Length + _lineBuffer.Length; }
        }
        private bool _finalized;
        private bool _ignoreLimit;
        public int LineLength { get; set; }
        public string WrappedLinePrefix = string.Empty;
        public string WrappedLineSuffix = string.Empty;

        public LimitedLineLengthStringBuilder() : this(150)
        {
        }

        public LimitedLineLengthStringBuilder(int lineLength)
        {
            _builder = new StringBuilder();
            _lineBuffer = new StringBuilder(lineLength);
            LineLength = lineLength;
        }

        public LimitedLineLengthStringBuilder Nonbreaking() { _ignoreLimit = true; return this; }

        public LimitedLineLengthStringBuilder Append(string value)
        {
            return AppendFormat(value);
        }

        public LimitedLineLengthStringBuilder AppendFormat(string value, params object[] args)
        {
            if (_finalized)
                throw new InvalidOperationException("Cannot append after finalizing.");

            value = string.Format(value, args);
            if (!_ignoreLimit)
                FlushIfNeeded(value);

            _lineBuffer.Append(value);
            _ignoreLimit = false;
            return this;
        }

        public LimitedLineLengthStringBuilder AppendLine()
        {
            return AppendLine(string.Empty);
        }

        public LimitedLineLengthStringBuilder AppendLine(string value)
        {
            return AppendFormatLine(value);
        }

        public LimitedLineLengthStringBuilder AppendFormatLine(string value, params object[] args)
        {
            if (_finalized)
                throw new InvalidOperationException("Cannot append after finalizing.");

            value = string.Format(value, args);
            if (!_ignoreLimit)
                FlushIfNeeded(value);

            _lineBuffer.Append(value);
            _builder.AppendLine(_lineBuffer.ToString());
            _lineBuffer.Clear();
            _ignoreLimit = false;
            return this;
        }

        public string Finalize()
        {
            if (!_finalized && _lineBuffer.Length > 0)
                _builder.Append(_lineBuffer.ToString());

            _finalized = true;
            return _builder.ToString();
        }

        private void FlushIfNeeded(string value)
        {
            if (_lineBuffer.Length + value.Length > LineLength)
            {
                _builder.Append(_lineBuffer.ToString());
                _builder.AppendLine(WrappedLineSuffix);
                _lineBuffer.Clear();
                _lineBuffer.Append(WrappedLinePrefix);
            }
        }

        public LimitedLineLengthStringBuilder Remove(int startIndex, int length)
        {
            if (startIndex > Length - length)
                throw new IndexOutOfRangeException();

            if (startIndex > _builder.Length)
                _lineBuffer.Remove(startIndex - _builder.Length, length);
            else  if (startIndex <= _builder.Length - length)
                _builder.Remove(startIndex, length);
            else
            {
                _builder.Remove(startIndex, _builder.Length - startIndex);
                _lineBuffer.Remove(0, startIndex + length - _builder.Length);
            }

            return this;
        }
    }
}
