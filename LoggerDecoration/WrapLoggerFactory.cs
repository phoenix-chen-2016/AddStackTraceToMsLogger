using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace LoggerDecoration;
internal class WrapLoggerFactory(ILoggerFactory originFactory) : ILoggerFactory
{
	private readonly ConcurrentDictionary<string, ILogger> m_Loggers = new();

	public ILogger CreateLogger(string categoryName)
		=> m_Loggers.GetOrAdd(
			categoryName,
			name => new WrapLogger(originFactory.CreateLogger(name)));

	public void AddProvider(ILoggerProvider provider)
		=> originFactory.AddProvider(provider);

	public void Dispose()
		=> originFactory.Dispose();
}
