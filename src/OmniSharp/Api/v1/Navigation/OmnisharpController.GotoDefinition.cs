// ﻿using System;
// using System.Collections.Generic;
// using System.Composition;
// using System.Linq;
// using System.Threading;
// using System.Threading.Tasks;
// using Microsoft.AspNet.Mvc;
// using Microsoft.CodeAnalysis.FindSymbols;
// using Microsoft.CodeAnalysis.Text;
// //using static OmniSharp.Endpoints;
// using OmniSharp.Models;
// using OmniSharp.Roslyn;
//
// namespace OmniSharp
// {
//     public partial class OmnisharpController
//     {
//         [HttpPost("gotodefinition")]
//         public async Task<GotoDefinitionResponse> GotoDefinition(GotoDefinitionRequest request)
//         {
//             //_workspace.PluginHost.GetExport(Type exportType)
//             var definitions = _workspace.PluginHost.GetExports<IGotoDefintion>();
//             foreach (var instance in definitions)
//             {
//                 if (await instance.IsApplicableTo(request))
//                 {
//                     return await instance.GotoDefinition(request);
//                 }
//             }
//
//             return null;
//         }
//     }
// }
