using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using Newtonsoft.Json.Linq;
using System.Net;

namespace sk_chatgpt_azure_function
{
	public class GetWiki
	{
		private static readonly HttpClient _httpClient = new HttpClient();
		private readonly ILogger _logger;

		public GetWiki(ILoggerFactory loggerFactory)
		{
			_logger = loggerFactory.CreateLogger<GetWiki>();
		}

		[OpenApiOperation(operationId: "GetWikiText", tags: new[] { "ExecuteFunction" }, Description = "Get the text of a wiki page from a given query so it can be summarized by SummarizeWikiArticle")]
		[OpenApiParameter(name: "title", Description = "The title of a wikipedia article", Required = true, In = ParameterLocation.Query)]
		[OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "text/plain", bodyType: typeof(string), Description = "The text of the wiki article that can be passed to SummarizeWikiArticle")]
		[Function("GetWikiText")]

		public async Task<HttpResponseData> Run([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestData req)
		{
			string article = await FetchWikipediaArticleContent(req.Query["title"]);

			if (article == "Article content not found.")
			{
				HttpResponseData response = req.CreateResponse(HttpStatusCode.BadRequest);
				response.Headers.Add("Content-Type", "application/json");
				response.WriteString("Article content not found.");
				_logger.LogInformation($"A Wiki article for {req.Query["title"]} was not found");

				return response;
			}
			else
			{
				HttpResponseData response = req.CreateResponse(HttpStatusCode.OK);
				response.Headers.Add("Content-Type", "text/plain");

				//Return the first 10,000 items in the article
				article = article.Substring(0, Math.Min(article.Length, 10000));
				response.WriteString(article);

				_logger.LogInformation($"A Wiki article for {req.Query["title"]} was found");

				return response;
			}
		}

		private static async Task<string> FetchWikipediaArticleContent(string query)
		{
			var url = $"https://en.wikipedia.org/w/api.php?format=json&action=query&prop=revisions&rvprop=content&redirects=1&titles={query}";

			var responseString = await _httpClient.GetStringAsync(url);
			var json = JObject.Parse(responseString);
			var pages = json["query"]["pages"];

			foreach (var page in pages)
			{
				var revisions = page.First["revisions"];
				if (revisions != null)
				{
					var content = revisions[0]["*"];
					return content.ToString();
				}
			}

			return "Article content not found.";
		}
	}
}
