using OneOf.Types;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Shapes;
using System.Xml.Linq;
using WebDav;

namespace AssemblyAiDemoApp
{
    internal class NextcloudManager
    {
        private const string SERVER_URL = "https://tennyleaz.duckdns.org/remote.php/dav/";
        private readonly NetworkCredential credential = new NetworkCredential("tennyleaz", "zWctK-qZRaE-K3dfK-q3mWL-nWpCz");
        private WebDavClient _client;

        public NextcloudManager()
        {
            WebDavClientParams para = new WebDavClientParams() { };
            para.Credentials = credential;
            para.BaseAddress = new Uri(SERVER_URL);
            _client = new WebDavClient(para);
        }

        public async Task List()
        {
            string url = "files/tennyleaz/Share/";
            var result = await _client.Propfind(url);
            if (result.IsSuccessful)
            {
                foreach (var res in result.Resources)
                {
                    Trace.WriteLine("Name: " + res.DisplayName);
                    Trace.WriteLine("Uri: " + res.Uri);
                    Trace.WriteLine("Is directory: " + res.IsCollection);
                    // etc.
                }
            }
        }

        public async Task<bool> Upload(string fileName)
        {
            if (!File.Exists(fileName))
            {
                return false;
            }

            using (FileStream fs = new FileStream(fileName, FileMode.Open))
            {
                string url = "files/tennyleaz/Share/" + fileName;
                // share permissons:
                // 0 - user share
                // 1 - group share
                // 2 - used internally
                // 3 - link shares
                var result = await _client.PutFile(url, fs);
                if (result.IsSuccessful)
                {
                    Trace.WriteLine("Upload successful");

                    await SetWebDavProperties(SERVER_URL + url);
                }
            }

            return false;
        }

        private async Task SetWebDavProperties(string fileUrl)
        {
            string xmlRequest = @"<?xml version=""1.0""?>
    <d:propertyupdate xmlns:d=""DAV:"" xmlns:oc=""http://owncloud.org/ns"">
      <d:set>
        <d:prop>
          <oc:share-types>
            <oc:share-type>3</oc:share-type>
          </oc:share-types>
        </d:prop>
      </d:set>
    </d:propertyupdate>";

            using (HttpClientHandler handler = new HttpClientHandler { Credentials = credential })
            using (HttpClient client = new HttpClient(handler))
            {
                HttpRequestMessage request = new HttpRequestMessage(new HttpMethod("PROPPATCH"), fileUrl)
                {
                    Content = new StringContent(xmlRequest, System.Text.Encoding.UTF8, "application/xml")
                };

                HttpResponseMessage response = await client.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    Console.WriteLine("Properties updated successfully.");
                }
                else
                {
                    Console.WriteLine($"Failed to update properties: {response.StatusCode}");
                    Console.WriteLine(await response.Content.ReadAsStringAsync());
                }
            }
        }
    }
}
