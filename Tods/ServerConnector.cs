using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json;
using RestSharp;

namespace Tods
{
    class ServerConnector : IServer
    {
        private const string ApiBase = "http://192.168.0.106:13579";

        public bool Register(string playerId)
        {
            var client = new RestClient(ApiBase);
            var request = new RestRequest("register/{id}", Method.GET);
            request.AddUrlSegment("id", playerId);

            return client.Execute<bool>(request).Data;
        }

        public bool Unregister(string playerId)
        {
            var client = new RestClient(ApiBase);
            var request = new RestRequest("unregister/{id}", Method.GET);
            request.AddUrlSegment("id", playerId);

            return client.Execute<bool>(request).Data;
        }

        public bool Post(Event evt)
        {
            var client = new RestClient(ApiBase);
            var request = new RestRequest("post", Method.POST);
            request.RequestFormat = DataFormat.Json;
            request.AddBody(evt);

            return client.Execute<bool>(request).Data;
        }

        public List<Event> Poll(string playerId)
        {
            var client = new RestClient(ApiBase);
            var request = new RestRequest("poll/{id}", Method.GET);
            request.AddUrlSegment("id", playerId);

            var response = client.Execute(request);
            if (response.ResponseStatus == ResponseStatus.Error)
            {
                Application.Exit();
            }
            Logger.Write($"Response {response.Content}");
            return JsonConvert.DeserializeObject<List<Event>>(response.Content);
        }

    }
}
