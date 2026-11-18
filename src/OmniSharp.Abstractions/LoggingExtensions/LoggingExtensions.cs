using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;

#nullable enable

namespace Microsoft.Extensions.Logging
{
    public static class LoggingExtensions
    {
        public static void Log(this ILogger logger, LogLevel logLevel, [InterpolatedStringHandlerArgument("logger", "logLevel")] LoggerInterpolatedStringHandler handler)
        {
            logger.Log(logLevel, handler.ToString());
        }
    }

    [InterpolatedStringHandler]
    public struct LoggerInterpolatedStringHandler
    {
        private readonly StringBuilder? _builder;
        public LoggerInterpolatedStringHandler(int literalLength, int formattedCount, ILogger logger, LogLevel level, out bool shouldAppend)
        {
            if (logger.IsEnabled(level))
            {
                shouldAppend = true;
                _builder = new(literalLength);
            }
            else
            {
                shouldAppend = false;
                _builder = null;
            }
        }

        public void AppendLiteral(string literal)
        {
            Debug.Assert(_builder != null);
            _builder!.Append(literal);
        }

        public void AppendFormatted<T>(T t)
        {
            Debug.Assert(_builder != null);
            _builder!.Append(t?.ToString());
        }

        public void AppendFormatted<T>(T t, int alignment, string format)
        {
            Debug.Assert(_builder != null);
            _builder!.Append(string.Format($"{{0,{alignment}:{format}}}", t));
        }

        public override string ToString()
        {
            return _builder?.ToString() ?? string.Empty;
        }
    }
}
