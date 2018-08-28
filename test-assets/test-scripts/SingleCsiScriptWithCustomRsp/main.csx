#r "System.Net.Http"

using System.Net.Http;
var client = new HttpClient();
Console.WriteLine(client.GetAsync("https://google.com"));