using System.Reflection;
using LoggerDecoration;
using Microsoft.Extensions.Logging;

namespace Microsoft.Extensions.DependencyInjection;

public static class ServiceCollectionExtensions
{
	public static IServiceCollection AddLoggerDecoration(this IServiceCollection services)
	{
		CallSiteInformation.AddCallSiteHiddenAssembly(typeof(LoggerExtensions).Assembly);
		CallSiteInformation.AddCallSiteHiddenAssembly(Assembly.GetExecutingAssembly());

		return services
			.Decorate<ILoggerFactory>((origin, _) => new WrapLoggerFactory(origin));
	}
}