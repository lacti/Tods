using System.Collections.Generic;
using System.IO;
using Nancy.IO;
using Nancy.ModelBinding;
using Newtonsoft.Json;

namespace Tods
{
    using Nancy;

    public class IndexModule : NancyModule
    {
        private static readonly SimpleServer Server = new SimpleServer();

        public IndexModule()
        {
            Get["/"] = parameters =>
            {
                return View["index"];
            };

            Get["/register/{playerId}"] = parameters =>
            {
                Logger.Write($"Player[{parameters.playerId}] is registered.");
                return ToJson(Server.Register(parameters.playerId));
            };

            Get["/unregister/{playerId}"] = parameters =>
            {
                Logger.Write($"Player[{parameters.playerId}] is unregistered.");
                return ToJson(Server.Unregister(parameters.playerId));
            };

            Post["/post"] = parameters =>
            {
                var body = Request.Body.ReadAsString();
                var evt = (Event) JsonConvert.DeserializeObject(body, typeof (Event));
                Logger.Write(evt.ToString());

                return ToJson(Server.Post(evt));
            };

            Get["/poll/{playerId}"] = parameters =>
            {
                return ToJson(Server.Poll(parameters.playerId));
            };
        }

        static string ToJson(object obj)
        {
            return JsonConvert.SerializeObject(obj);
        }
    }

    public static class RequestBodyExtensions
    {
        public static string ReadAsString(this RequestStream requestStream)
        {
            using (var reader = new StreamReader(requestStream))
            {
                return reader.ReadToEnd();
            }
        }
    }

}