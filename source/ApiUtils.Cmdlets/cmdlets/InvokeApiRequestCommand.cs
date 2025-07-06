#nullable enable
using System;
using System.Collections;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Management.Automation;
using ApiUtils.Models;
using ApiUtils.Runtime;

namespace ApiUtils.Cmdlets
{
    /// <summary>
    /// Issues an HTTP API request using a registered or provided <see cref="ApiSession"/> and returns the response.
    /// </summary>
    [Cmdlet(VerbsLifecycle.Invoke, "ApiRequest", SupportsShouldProcess = true, DefaultParameterSetName = "StandardMethod")]
    [OutputType(typeof(ApiResponse))]
    public class InvokeApiRequestCommand : WebRequestPSCmdlet
    {


    }

}
