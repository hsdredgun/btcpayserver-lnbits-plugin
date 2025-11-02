using BTCPayServer.Abstractions.Contracts;
using BTCPayServer.Abstractions.Models;
using BTCPayServer.Lightning;
using BTCPayServer.Lightning.LNbits;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace BTCPayServer.Plugins.LNbits
{
    public class LNbitsPlugin : IBTCPayServerPlugin
    {
        public string Identifier => "BTCPayServer.Plugins.LNbits";
        public string Name => "LNbits Lightning";
        public string Description => "Connect BTCPay Server to LNbits with webhook support";
        public Version Version => new Version(1, 1, 0);
        
        public bool SystemPlugin { get; set; } = false;
        
        public IBTCPayServerPlugin.PluginDependency[] Dependencies => Array.Empty<IBTCPayServerPlugin.PluginDependency>();

        public void Execute(IServiceCollection services)
        {
            // Register HTTP context accessor for getting BTCPay URL
            services.AddHttpContextAccessor();
            
            // Register the Lightning connection string handler
            services.AddSingleton<ILightningConnectionStringHandler, LNbitsConnectionStringHandler>();
        }

        public void Execute(IApplicationBuilder applicationBuilder, IServiceProvider serviceProvider)
        {
            // This method is called after services are configured
            // We don't need any additional setup here for now
        }
    }
}