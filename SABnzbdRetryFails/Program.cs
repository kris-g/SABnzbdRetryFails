using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using Newtonsoft.Json.Linq;

namespace SABnzbdRetryFails
{
	class Program
	{
		public static void Main(string[] args)
		{
			string server = null;
			string apiKey = null;

			foreach (string str in args) {
				if (str.StartsWith("/server:", StringComparison.InvariantCultureIgnoreCase)) {
					server = str.Substring(8);
				}
				if (str.StartsWith("/apikey:", StringComparison.InvariantCultureIgnoreCase)) {
					apiKey = str.Substring(8);
				}
				if (str.StartsWith("/?") || str.StartsWith("/help", StringComparison.InvariantCultureIgnoreCase)) {
					PrintUsage();
					return;
				}
			}

			if (server == null || apiKey == null) {
				PrintUsage();
				return;
			}

			string separator = new string('-', 50);
			Log(string.Format("Start run at {0}", DateTime.Now));
			Log(string.Format("Querying server: {0}", server));
			Log(string.Format("with API key: {0}", apiKey));
			Log();

			string urlHistory = string.Format("{0}/sabnzbd/api?apikey={1}&mode=history&output=json", server, apiKey);
			string jsonHistory = GetUrlContent(urlHistory);
			JObject jsonObj = JObject.Parse(jsonHistory);

			JToken jsonRoot = jsonObj["history"];
			if (jsonRoot != null) {
				JToken jsonSlots = jsonRoot["slots"];

				if (jsonSlots != null) {
					foreach (JToken jsonSlot in jsonSlots) {
						string status = jsonSlot.Value<string>("status");

						if (status == "Failed") {
							Log(separator);
							Log("Found failed item");
							Log();

							string cat = jsonSlot.Value<string>("category");
							string nzoId = jsonSlot.Value<string>("nzo_id");
							string nzbName = jsonSlot.Value<string>("nzb_name");

							// retry API sucks
							// even though web UI shows retry option, the API doesn't always support it
							// just delete and re-add while trying to preserve category
							if (nzoId != null && nzbName != null) {
								Log(string.Format("Failed item name: {0}, nzo id: {1}\n\r", nzbName, nzoId));

								string urlDelete = string.Format("{0}/sabnzbd/api?apikey={1}&mode=history&name=delete&value={2}", server, apiKey, nzoId);
								Log("Deleting failed item with action:");
								Log(urlDelete);
								Log();

								GetUrlContent(urlDelete);

								string catQuery = string.Empty;
								if (!string.IsNullOrEmpty(cat)) {
									catQuery = string.Format("&cat={0}", cat);
								}
								string urlAdd = string.Format("{0}/sabnzbd/api?apikey={1}&mode=addurl&name={2}{3}", server, apiKey, nzbName, catQuery);
								Log("Re-adding failed item with action:");
								Log(urlAdd);
								Log();

								GetUrlContent(urlAdd);
							}
						}
					}
				}
			}

			Log(separator);
		}

		public static void Log()
		{
			Log(string.Empty);
		}

		public static void Log(string msg)
		{
			Console.WriteLine(msg);
			File.AppendAllText("log.txt", msg + Environment.NewLine);
		}

		public static string GetUrlContent(string url)
		{
			HttpWebRequest request = (HttpWebRequest) HttpWebRequest.Create(url);

			WebResponse response = request.GetResponse();
			Stream stream = response.GetResponseStream();
			StreamReader reader = new StreamReader(stream);
			string result = reader.ReadToEnd();

			reader.Close();
			stream.Close();
			response.Close();

			return result;
		}

		public static void PrintUsage()
		{
			StringBuilder sb = new StringBuilder();
			sb.AppendLine(string.Format("SABnzbd Retry Fails (v{0}):", Assembly.GetEntryAssembly().GetName().Version));
			sb.AppendLine("Queries SABnzbd for failed recent jobs and queues them do retry");
			sb.AppendLine();
			sb.AppendLine("Usage: SABnzbdRetryFails.exe /server:[SERVER URL] /apikey:[APIKEY]");
			sb.AppendLine();

			Console.WriteLine(sb.ToString());
		}
	}
}
