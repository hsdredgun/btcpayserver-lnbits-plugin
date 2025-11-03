// File: LNbitsPlugin.cs
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
            services.AddHttpContextAccessor();
            services.AddSingleton<ILightningConnectionStringHandler, LNbitsConnectionStringHandler>();
            services.AddControllers().AddApplicationPart(typeof(LNbitsPlugin).Assembly);
        }

        public void Execute(IApplicationBuilder app, IServiceProvider sp) { }
    }
}