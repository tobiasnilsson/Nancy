namespace Nancy.Diagnostics
{
    using System;
    using System.Collections.Generic;
    using System.IO;

    using Bootstrapper;
    using ModelBinding;
    using Routing;

    public static class DiagnosticsHook
    {
        internal const string ControlPanelPrefix = "/_Nancy";

        internal const string ResourcePrefix = ControlPanelPrefix + "/Resources/";
        
        private const string PipelineKey = "__Diagnostics";

        public static void Enable(DiagnosticsConfiguration diagnosticsConfiguration, IPipelines pipelines, IEnumerable<IDiagnosticsProvider> providers, IRootPathProvider rootPathProvider, IEnumerable<ISerializer> serializers, IRequestTracing requestTracing, NancyInternalConfiguration configuration, IModelBinderLocator modelBinderLocator)
        {
            var keyGenerator = new DefaultModuleKeyGenerator();
            var diagnosticsModuleCatalog = new DiagnosticsModuleCatalog(keyGenerator, providers, rootPathProvider, requestTracing, configuration, diagnosticsConfiguration);

            var diagnosticsRouteCache = new RouteCache(diagnosticsModuleCatalog, keyGenerator, new DefaultNancyContextFactory());

            var diagnosticsRouteResolver = new DefaultRouteResolver(
                diagnosticsModuleCatalog,
                new DefaultRoutePatternMatcher(),
                new DiagnosticsModuleBuilder(rootPathProvider, serializers, modelBinderLocator),
                diagnosticsRouteCache);
            
            pipelines.BeforeRequest.AddItemToStartOfPipeline(
                new PipelineItem<Func<NancyContext, Response>>(
                    PipelineKey,
                    ctx =>
                    {
                        if (!ctx.ControlPanelEnabled)
                        {
                            return null;
                        }

                        if (!ctx.Request.Path.StartsWith(ControlPanelPrefix, StringComparison.OrdinalIgnoreCase))
                        {
                            return null;
                        }

                        if (ctx.Request.Path.StartsWith(ResourcePrefix, StringComparison.OrdinalIgnoreCase))
                        {
                            var resourceNamespace = "Nancy.Diagnostics.Resources";

                            var path = Path.GetDirectoryName(ctx.Request.Url.Path.Replace(ResourcePrefix, string.Empty)) ?? string.Empty;
                            if (!string.IsNullOrEmpty(path))
                            {
                                resourceNamespace += string.Format(".{0}", path.Replace('\\', '.'));
                            }

                            return new EmbeddedFileResponse(
                                typeof(DiagnosticsHook).Assembly,
                                resourceNamespace,
                                Path.GetFileName(ctx.Request.Url.Path));
                        }

                        return diagnosticsConfiguration.Valid
                                   ? ExecuteDiagnosticsModule(ctx, diagnosticsRouteResolver)
                                   : GetDiagnosticsHelpView();
                    }));
        }

        public static void Disable(IPipelines pipelines)
        {
            pipelines.BeforeRequest.RemoveByName(PipelineKey);
        }

        private static Response GetDiagnosticsHelpView()
        {
            var renderer = new DiagnosticsViewRenderer();

            return renderer["help"];
        }

        private static Response ExecuteDiagnosticsModule(NancyContext ctx, IRouteResolver routeResolver)
        {
            // TODO - duplicate the context and strip out the "_/Nancy" bit so we don't need to use it in the module
            var resolveResult = routeResolver.Resolve(ctx);

            ctx.Parameters = resolveResult.Item2;
            var resolveResultPreReq = resolveResult.Item3;
            var resolveResultPostReq = resolveResult.Item4;
            ExecuteRoutePreReq(ctx, resolveResultPreReq);

            if (ctx.Response == null)
            {
                ctx.Response = resolveResult.Item1.Invoke(resolveResult.Item2);
            }

            if (ctx.Request.Method.ToUpperInvariant() == "HEAD")
            {
                ctx.Response = new HeadResponse(ctx.Response);
            }

            if (resolveResultPostReq != null)
            {
                resolveResultPostReq.Invoke(ctx);
            }

            // If we duplicate the context this makes more sense :)
            return ctx.Response;
        }

        private static void ExecuteRoutePreReq(NancyContext context, Func<NancyContext, Response> resolveResultPreReq)
        {
            if (resolveResultPreReq == null)
            {
                return;
            }

            var resolveResultPreReqResponse = resolveResultPreReq.Invoke(context);

            if (resolveResultPreReqResponse != null)
            {
                context.Response = resolveResultPreReqResponse;
            }
        }
    }
}