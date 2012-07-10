﻿using System;
using System.Collections.Generic;
using Nancy.ViewEngines;

namespace Nancy.Responses.Negotiation
{
    public class ViewProcessor : IResponseProcessor
    {
        private readonly IViewFactory viewFactory;

        public ViewProcessor(IViewFactory viewFactory)
        {
            this.viewFactory = viewFactory;
        }

        public IEnumerable<Tuple<string, MediaRange>> ExtensionMappings
        {
            get { yield break; }
        }

        public string GetFullOutputContentType(MediaRange requestedMediaRange, NancyContext context)
        {
            return "text/html";
        }

        public ProcessorMatch CanProcess(MediaRange requestedMediaRange, NancyContext context)
        {
            var matchingContentType = requestedMediaRange.Type.Equals("text") && requestedMediaRange.Subtype.Equals("html");

            return matchingContentType 
                ? new ProcessorMatch { ModelResult = MatchResult.DontCare, RequestedContentTypeResult = MatchResult.ExactMatch } 
                : new ProcessorMatch();
        }

        public Response Process(MediaRange requestedMediaRange, NancyContext context)
        {
            return this.viewFactory.RenderView(
                            context.NegotiationContext.ViewName, 
                            context.NegotiationContext.GetModelForMediaRange(requestedMediaRange), 
                            GetViewLocationContext(context));
        }

        private static ViewLocationContext GetViewLocationContext(NancyContext context)
        {
            return new ViewLocationContext
                       {
                           Context = context,
                           ModuleName = context.NegotiationContext.ModuleName,
                           ModulePath = context.NegotiationContext.ModulePath
                       };
        }
    }
}