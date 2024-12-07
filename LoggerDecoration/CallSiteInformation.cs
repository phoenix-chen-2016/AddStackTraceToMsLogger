using System.Diagnostics;
using System.Reflection;

namespace LoggerDecoration;

internal class CallSiteInformation
{
	private static readonly Lock lockObject = new();
	private static HashSet<Assembly> m_HiddenAssemblies = [];
	private static HashSet<Type> m_HiddenTypes = [];

	/// <summary>
	/// Gets the number index of the stack frame that represents the user
	/// code (not the NLog code).
	/// </summary>
	public int UserStackFrameNumber { get; private set; }

	/// <summary>
	/// Legacy attempt to skip async MoveNext, but caused source file line number to be lost
	/// </summary>
	public int? UserStackFrameNumberLegacy { get; private set; }

	/// <summary>
	/// Gets the entire stack trace.
	/// </summary>
	public StackTrace? StackTrace { get; private set; }

	public string? CallerClassName { get; internal set; }

	public string? CallerMethodName { get; private set; }

	public string? CallerFilePath { get; private set; }

	public int? CallerLineNumber { get; private set; }

	/// <summary>
	/// Adds the given assembly which will be skipped
	/// when NLog is trying to find the calling method on stack trace.
	/// </summary>
	/// <param name="assembly">The assembly to skip.</param>
	public static void AddCallSiteHiddenAssembly(Assembly assembly)
	{
		if (m_HiddenAssemblies.Contains(assembly) || assembly is null)
			return;

		lock (lockObject)
		{
			if (m_HiddenAssemblies.Contains(assembly))
				return;

			m_HiddenAssemblies = new HashSet<Assembly>(m_HiddenAssemblies)
			{
				assembly
			};
		}
	}

	public static void AddCallSiteHiddenClassType(Type classType)
	{
		if (m_HiddenTypes.Contains(classType) || classType is null)
			return;

		lock (lockObject)
		{
			if (m_HiddenTypes.Contains(classType))
				return;

			m_HiddenTypes = new HashSet<Type>(m_HiddenTypes)
			{
				classType
			};
		}
	}

	/// <summary>
	/// Sets the stack trace for the event info.
	/// </summary>
	/// <param name="stackTrace">The stack trace.</param>
	/// <param name="userStackFrame">Index of the first user stack frame within the stack trace.</param>
	/// <param name="loggerType">Type of the logger or logger wrapper. This is still Logger if it's a subclass of Logger.</param>
	public void SetStackTrace(StackTrace stackTrace, int? userStackFrame = null, Type? loggerType = null)
	{
		StackTrace = stackTrace;
		if (!userStackFrame.HasValue && stackTrace != null)
		{
			var stackFrames = stackTrace.GetFrames();
			var firstUserFrame = loggerType != null ? FindCallingMethodOnStackTrace(stackFrames, loggerType) : 0;
			var firstLegacyUserFrame = firstUserFrame.HasValue ? SkipToUserStackFrameLegacy(stackFrames, firstUserFrame.Value) : firstUserFrame;
			UserStackFrameNumber = firstUserFrame ?? 0;
			UserStackFrameNumberLegacy = firstLegacyUserFrame != firstUserFrame ? firstLegacyUserFrame : null;
		}
		else
		{
			UserStackFrameNumber = userStackFrame ?? 0;
			UserStackFrameNumberLegacy = null;
		}
	}

	/// <summary>
	/// Sets the details retrieved from the Caller Information Attributes
	/// </summary>
	/// <param name="callerClassName"></param>
	/// <param name="callerMethodName"></param>
	/// <param name="callerFilePath"></param>
	/// <param name="callerLineNumber"></param>
	public void SetCallerInfo(string callerClassName, string callerMethodName, string callerFilePath, int callerLineNumber)
	{
		CallerClassName = callerClassName;
		CallerMethodName = callerMethodName;
		CallerFilePath = callerFilePath;
		CallerLineNumber = callerLineNumber;
	}

	public MethodBase? GetCallerStackFrameMethod(int skipFrames)
	{
		var frame = StackTrace?.GetFrame(UserStackFrameNumber + skipFrames);

		return frame is null ? null : StackTraceUsageUtils.GetStackMethod(frame);
	}

	public string GetCallerClassName(MethodBase? method, bool includeNameSpace, bool cleanAsyncMoveNext, bool cleanAnonymousDelegates)
	{
		if (!string.IsNullOrEmpty(CallerClassName))
		{
			if (includeNameSpace)
			{
				return CallerClassName;
			}
			else
			{
				int lastDot = CallerClassName.LastIndexOf('.');

				return lastDot < 0 || lastDot >= CallerClassName.Length - 1
					? CallerClassName
					: CallerClassName[(lastDot + 1)..];
			}
		}

		method ??= GetCallerStackFrameMethod(0);
		if (method is null)
			return string.Empty;

		cleanAsyncMoveNext = cleanAsyncMoveNext || UserStackFrameNumberLegacy.HasValue;
		cleanAnonymousDelegates = cleanAnonymousDelegates || UserStackFrameNumberLegacy.HasValue;

		return StackTraceUsageUtils.GetStackFrameMethodClassName(
			method,
			includeNameSpace,
			cleanAsyncMoveNext,
			cleanAnonymousDelegates) ?? string.Empty;
	}

	public string GetCallerMethodName(MethodBase? method, bool includeMethodInfo, bool cleanAsyncMoveNext, bool cleanAnonymousDelegates)
	{
		if (!string.IsNullOrEmpty(CallerMethodName))
			return CallerMethodName;

		method ??= GetCallerStackFrameMethod(0);
		if (method is null)
			return string.Empty;

		cleanAsyncMoveNext = cleanAsyncMoveNext || UserStackFrameNumberLegacy.HasValue;
		cleanAnonymousDelegates = cleanAnonymousDelegates || UserStackFrameNumberLegacy.HasValue;

		return StackTraceUsageUtils.GetStackFrameMethodName(
			method,
			includeMethodInfo,
			cleanAsyncMoveNext,
			cleanAnonymousDelegates) ?? string.Empty;
	}

	public string GetCallerFilePath(int skipFrames)
	{
		if (!string.IsNullOrEmpty(CallerFilePath))
			return CallerFilePath;

		var frame = StackTrace?.GetFrame(UserStackFrameNumber + skipFrames);

		return frame?.GetFileName() ?? string.Empty;
	}

	public int GetCallerLineNumber(int skipFrames)
	{
		if (CallerLineNumber.HasValue)
			return CallerLineNumber.Value;

		var frame = StackTrace?.GetFrame(UserStackFrameNumber + skipFrames);

		return frame?.GetFileLineNumber() ?? 0;
	}

	internal static bool IsHiddenAssembly(Assembly assembly)
		=> m_HiddenAssemblies.Count != 0 && m_HiddenAssemblies.Contains(assembly);

	internal static bool IsHiddenClassType(Type type)
		=> m_HiddenTypes.Count != 0 && m_HiddenTypes.Contains(type);

	/// <summary>
	///  Finds first user stack frame in a stack trace
	/// </summary>
	/// <param name="stackFrames">The stack trace of the logging method invocation</param>
	/// <param name="loggerType">Type of the logger or logger wrapper. This is still Logger if it's a subclass of Logger.</param>
	/// <returns>Index of the first user stack frame or 0 if all stack frames are non-user</returns>
	private static int? FindCallingMethodOnStackTrace(StackFrame[] stackFrames, Type loggerType)
	{
		if (stackFrames is null || stackFrames.Length == 0)
			return null;

		int? firstStackFrameAfterLogger = null;
		int? firstUserStackFrame = null;
		for (int i = 0; i < stackFrames.Length; ++i)
		{
			var stackFrame = stackFrames[i];
			var stackMethod = StackTraceUsageUtils.GetStackMethod(stackFrame);
			if (SkipStackFrameWhenHidden(stackMethod))
				continue;

			if (!firstUserStackFrame.HasValue)
				firstUserStackFrame = i;

			if (SkipStackFrameWhenLoggerType(stackMethod, loggerType))
			{
				firstStackFrameAfterLogger = null;
				continue;
			}

			if (!firstStackFrameAfterLogger.HasValue)
				firstStackFrameAfterLogger = i;
		}

		return firstStackFrameAfterLogger ?? firstUserStackFrame;
	}

	/// <summary>
	/// This is only done for legacy reason, as the correct method-name and line-number should be extracted from the MoveNext-StackFrame
	/// </summary>
	/// <param name="stackFrames">The stack trace of the logging method invocation</param>
	/// <param name="firstUserStackFrame">Starting point for skipping async MoveNext-frames</param>
	private static int SkipToUserStackFrameLegacy(StackFrame[] stackFrames, int firstUserStackFrame)
	{
#if !NET35 && !NET40
		for (int i = firstUserStackFrame; i < stackFrames.Length; ++i)
		{
			var stackFrame = stackFrames[i];
			var stackMethod = StackTraceUsageUtils.GetStackMethod(stackFrame);
			if (SkipStackFrameWhenHidden(stackMethod))
				continue;

			if (stackMethod?.Name == "MoveNext" && stackFrames.Length > i)
			{
				var nextStackFrame = stackFrames[i + 1];
				var nextStackMethod = StackTraceUsageUtils.GetStackMethod(nextStackFrame);
				var declaringType = nextStackMethod?.DeclaringType;
				if (declaringType?.Namespace == "System.Runtime.CompilerServices" || declaringType == typeof(System.Threading.ExecutionContext))
				{
					//async, search further
					continue;
				}
			}

			return i;
		}
#endif
		return firstUserStackFrame;
	}

	/// <summary>
	/// Skip StackFrame when from hidden Assembly / ClassType
	/// </summary>
	private static bool SkipStackFrameWhenHidden(MethodBase? stackMethod)
	{
		var assembly = StackTraceUsageUtils.LookupAssemblyFromMethod(stackMethod);

		return assembly is null
			|| IsHiddenAssembly(assembly)
			|| stackMethod is null
			|| stackMethod.DeclaringType is null
			|| IsHiddenClassType(stackMethod.DeclaringType);
	}

	/// <summary>
	/// Skip StackFrame when type of the logger
	/// </summary>
	private static bool SkipStackFrameWhenLoggerType(MethodBase? stackMethod, Type loggerType)
	{
		var declaringType = stackMethod?.DeclaringType;

		var isLoggerType = declaringType != null
			&& (loggerType == declaringType
				|| declaringType.IsSubclassOf(loggerType)
				|| loggerType.IsAssignableFrom(declaringType));

		return isLoggerType;
	}
}