using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNet.Mvc;
using OmniSharp.Models;

namespace OmniSharp
{
    public partial class OmnisharpController
    {
        [HttpPost("navigateup")]
        public async Task<NavigateResponse> NavigateUp(Request request)
        {
            return await Navigate(request, IsCloserNodeUp);
        }

        [HttpPost("navigatedown")]
        public async Task<NavigateResponse> NavigateDown(Request request)
        {
            return await Navigate(request, IsCloserNodeDown);
        }

        private async Task<NavigateResponse> Navigate(Request request, Func<FileMemberElement, FileMemberElement, Request, bool> IsCloserNode)
        {
            var stack = new List<FileMemberElement>(await StructureComputer.Compute(_workspace.GetDocuments(request.FileName)));
            var response = new NavigateResponse();
            //Retain current line in case we dont need to navigate.
            response.Line = request.Line;
            response.Column = request.Column;

            FileMemberElement closestNode = null;
            FileMemberElement thisNode = null;
            while (stack.Count > 0)
            {
                var node = stack[0];
                stack.Remove(node);
                var isCloserNode = IsCloserNode(node, closestNode, request);
                if (isCloserNode)
                {
                    closestNode = node;
                }
                if (node.Location.Line == request.Line)
                {
                    thisNode = node;
                }
                stack.AddRange(node.ChildNodes);
            }

            //If there is a closest node, use its line and column.
            //or if we are on the last node, adjust column.
            //if we are above the first or below the last node, do nothing.
            if (closestNode != null)
            {
                response.Line = closestNode.Location.Line;
                response.Column = closestNode.Location.Column;
            }
            else if (thisNode != null)
            {
                response.Column = thisNode.Location.Column;
            }
            return response;
        }

        private bool IsCloserNodeUp(FileMemberElement candidateClosestNode, FileMemberElement closestNode, Request request)
        {
            return ((candidateClosestNode.Location.Line < request.Line) && (closestNode == null || candidateClosestNode.Location.Line > closestNode.Location.Line));
        }

        private bool IsCloserNodeDown(FileMemberElement candidateClosestNode, FileMemberElement closestNode, Request request)
        {
            return ((candidateClosestNode.Location.Line > request.Line) && (closestNode == null || candidateClosestNode.Location.Line < closestNode.Location.Line));
        }
    }
}
