using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace LoggerDecoration;

internal class WrapLogger(ILogger originLogger) : ILogger
{
	public IDisposable? BeginScope<TState>(TState state) where TState : notnull
		=> originLogger.BeginScope(state);

	public bool IsEnabled(LogLevel logLevel)
		=> originLogger.IsEnabled(logLevel);

	public void Log<TState>(
		LogLevel logLevel,
		EventId eventId,
		TState state,
		Exception? exception,
		Func<TState, Exception?, string> formatter)
	{
		var callSiteInfo = new CallSiteInformation();
		callSiteInfo.SetStackTrace(new StackTrace(true), loggerType: typeof(WrapLogger));

		var lineNumber = callSiteInfo.GetCallerLineNumber(0);
		var fileName = callSiteInfo.GetCallerFilePath(0);
		var methodName = callSiteInfo.GetCallerMethodName(null, false, true, true);
		var className = callSiteInfo.GetCallerClassName(null, true, true, true);

		var traceAttributes = new List<KeyValuePair<string, object>>()
		{
			new("LineNumber", lineNumber),
			new("FileName", fileName),
			new("MethodName", methodName),
			new("ClassName", className)
		};

		if (state is IReadOnlyList<KeyValuePair<string, object>> values)
		{
			originLogger.Log(
				logLevel,
				eventId,
				values.Concat(traceAttributes).ToList().AsReadOnly(),
				exception,
				(_, _) => formatter(state, exception));
		}
		else
		{
			traceAttributes.Add(new("State", state!));

			originLogger.Log(
				logLevel,
				eventId,
				traceAttributes.AsReadOnly(),
				exception,
				(_, _) => formatter(state, exception));
		}
	}
}