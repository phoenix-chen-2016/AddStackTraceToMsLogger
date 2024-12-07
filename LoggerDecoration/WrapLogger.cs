﻿using System.Diagnostics;
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

		if (state is IReadOnlyList<KeyValuePair<string, object>> values)
		{
			originLogger.Log(logLevel, eventId, values.Concat([
				new KeyValuePair<string, object>("LineNumber", lineNumber),
				new KeyValuePair<string, object>("FileName", fileName),
				new KeyValuePair<string, object>("MethodName", methodName),
				new KeyValuePair<string, object>("ClassName", className)
			]).ToList().AsReadOnly(), exception, (IReadOnlyList<KeyValuePair<string, object>> w, Exception? ex) => formatter(state, ex));
		}
		else
		{
			originLogger.Log(logLevel, eventId, new List<KeyValuePair<string, object?>>()
			{
				new KeyValuePair<string, object>("LineNumber", lineNumber),
				new KeyValuePair<string, object>("FileName", fileName),
				new KeyValuePair<string, object>("MethodName", methodName),
				new KeyValuePair<string, object>("ClassName", className),
				new KeyValuePair<string, object>("State", state!)
			}.AsReadOnly(), exception, (IReadOnlyList<KeyValuePair<string, object>> w, Exception? ex) => formatter(state, ex));
		}
	}
}