using System.Diagnostics;
using System.Reflection;

namespace LoggerDecoration;

internal static class StackTraceUsageUtils
{
	private static readonly Assembly wrapAssembly = typeof(StackTraceUsageUtils).Assembly;
	private static readonly Assembly mscorlibAssembly = typeof(string).Assembly;
	private static readonly Assembly systemAssembly = typeof(Debug).Assembly;

	public static string? GetStackFrameMethodName(MethodBase? method, bool includeMethodInfo, bool cleanAsyncMoveNext, bool cleanAnonymousDelegates)
	{
		if (method is null)
			return null;

		string methodName = method.Name;

		var callerClassType = method.DeclaringType;
		if (cleanAsyncMoveNext && methodName == "MoveNext" && callerClassType?.DeclaringType != null && callerClassType.Name.IndexOf('<') == 0)
		{
			// NLog.UnitTests.LayoutRenderers.CallSiteTests+<CleanNamesOfAsyncContinuations>d_3'1.MoveNext
			int endIndex = callerClassType.Name.IndexOf('>', 1);
			if (endIndex > 1)
			{
				methodName = callerClassType.Name.Substring(1, endIndex - 1);
				if (methodName.IndexOf('<') == 0)
					methodName = methodName.Substring(1, methodName.Length - 1);    // Local functions, and anonymous-methods in Task.Run()
			}
		}

		// Clean up the function name if it is an anonymous delegate
		// <.ctor>b__0
		// <Main>b__2
		if (cleanAnonymousDelegates && (methodName.IndexOf('<') == 0 && methodName.IndexOf("__", StringComparison.Ordinal) >= 0 && methodName.IndexOf('>') >= 0))
		{
			int startIndex = methodName.IndexOf('<') + 1;
			int endIndex = methodName.IndexOf('>');
			methodName = methodName.Substring(startIndex, endIndex - startIndex);
		}

		if (includeMethodInfo && methodName == method.Name)
		{
			methodName = method.ToString();
		}

		return methodName;
	}

	public static string GetStackFrameMethodClassName(MethodBase method, bool includeNameSpace, bool cleanAsyncMoveNext, bool cleanAnonymousDelegates)
	{
		if (method is null)
			return null;

		var callerClassType = method.DeclaringType;
		if (cleanAsyncMoveNext
		  && method.Name == "MoveNext"
		  && callerClassType?.DeclaringType != null
		  && callerClassType.Name?.IndexOf('<') == 0
		  && callerClassType.Name.IndexOf('>', 1) > 1)
		{
			// NLog.UnitTests.LayoutRenderers.CallSiteTests+<CleanNamesOfAsyncContinuations>d_3'1
			callerClassType = callerClassType.DeclaringType;
		}

		string className = includeNameSpace ? callerClassType?.FullName : callerClassType?.Name;
		if (cleanAnonymousDelegates && className?.IndexOf("<>", StringComparison.Ordinal) >= 0)
		{
			if (!includeNameSpace && callerClassType.DeclaringType != null && callerClassType.IsNested)
			{
				className = callerClassType.DeclaringType.Name;
			}
			else
			{
				// NLog.UnitTests.LayoutRenderers.CallSiteTests+<>c__DisplayClassa
				int index = className.IndexOf("+<>", StringComparison.Ordinal);
				if (index >= 0)
				{
					className = className.Substring(0, index);
				}
			}
		}

		if (includeNameSpace && className?.IndexOf('.') == -1)
		{
			var typeNamespace = GetNamespaceFromTypeAssembly(callerClassType);
			className = string.IsNullOrEmpty(typeNamespace) ? className : string.Concat(typeNamespace, ".", className);
		}

		return className;
	}

	private static string GetNamespaceFromTypeAssembly(Type callerClassType)
	{
		var classAssembly = callerClassType.Assembly;
		if (classAssembly != null && classAssembly != mscorlibAssembly && classAssembly != systemAssembly)
		{
			var assemblyFullName = classAssembly.FullName;
			if (assemblyFullName?.IndexOf(',') >= 0 && !assemblyFullName.StartsWith("System.", StringComparison.Ordinal) && !assemblyFullName.StartsWith("Microsoft.", StringComparison.Ordinal))
			{
				return assemblyFullName.Substring(0, assemblyFullName.IndexOf(','));
			}
		}

		return null;
	}

	[System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming - Allow callsite logic", "IL2026")]
	public static MethodBase? GetStackMethod(StackFrame stackFrame)
	{
		return stackFrame.GetMethod();
	}

	/// <summary>
	/// Returns the assembly from the provided StackFrame (If not internal assembly)
	/// </summary>
	/// <returns>Valid assembly, or null if assembly was internal</returns>
	public static Assembly? LookupAssemblyFromMethod(MethodBase? method)
	{
		var assembly = method?.DeclaringType?.Assembly ?? method?.Module?.Assembly;

		// skip stack frame if the method declaring type assembly is from hidden assemblies list
		return assembly == wrapAssembly || assembly == mscorlibAssembly || assembly == systemAssembly ? null : assembly;
	}
}