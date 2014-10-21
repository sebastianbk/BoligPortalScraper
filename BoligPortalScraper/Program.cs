using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HtmlAgilityPack;
using System.Net;
using System.Collections.Specialized;
using System.Threading;
using Newtonsoft.Json.Linq;

namespace BoligPortalScraper
{
    class Program
    {
        static void Main(string[] args)
        {
            BoligPortalDataContext db = new BoligPortalDataContext();
            CookieContainer cookieContainer = new CookieContainer();
            WebClientEx client = new WebClientEx(cookieContainer);
            client.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 6.3; WOW64; rv:29.0) Gecko/20100101 Firefox/29.0");

            Console.WriteLine("Logging in...");
            NameValueCollection values = new NameValueCollection();
            values.Add("LoginForm[username]", "sine.gaunitz@gmail.com");
            values.Add("LoginForm[password]", "sintin88");
            byte[] loginByteResponse = client.UploadValues("http://www.boligportal.dk/authorization/loginAjax", "POST", values);
            Console.WriteLine("Logged in!");

            while (true)
            {
                Console.WriteLine("Getting results page...");
                string resultsPage = client.DownloadString("http://www.boligportal.dk/lejebolig/soeg_leje_bolig.php?do=showFromPersonligMenu");
                HtmlDocument doc = new HtmlDocument();
                doc.LoadHtml(resultsPage);
                Console.WriteLine("Got results!");

                var searchResults = doc.DocumentNode.SelectNodes("//table[@class='lejebolig ']");

                foreach (var result in searchResults)
                {
                    string link = result.SelectSingleNode(".//a").Attributes.First().Value;
                    if (db.Apartments.SingleOrDefault(x => x.Link == link) != null)
                        continue;
                    string headline = result.SelectSingleNode(".//a").InnerText;
                    Console.WriteLine("Flat: " + headline);

                    Apartment apartment = new Apartment();
                    apartment.Headline = headline.Replace("&nbsp;", "");
                    apartment.Description = result.SelectSingleNode(".//div[@id='bdesc']").InnerText.Replace("&nbsp;", "");
                    apartment.Post = result.SelectSingleNode(".//div[@id='bpost']").InnerText.Replace("&nbsp;", "");
                    apartment.Size = result.SelectSingleNode(".//td[@id='bsize']").InnerText.Split(' ').First().Replace("&nbsp;", "");
                    apartment.Type = result.SelectSingleNode(".//td[@id='btype']").InnerText.Replace("&nbsp;", "");
                    apartment.Price = result.SelectSingleNode(".//td[@id='bprice']").InnerText.Replace("&nbsp;", "");
                    apartment.Link = link;
                    db.Apartments.InsertOnSubmit(apartment);

                    WebClient smsClient = new WebClient();
                    WebClient apiClient = new WebClient();
                    apiClient.Encoding = Encoding.UTF8;
                    apiClient.Headers.Add("Content-Type", "application/json");
                    string googleResponse = apiClient.UploadString("https://www.googleapis.com/urlshortener/v1/url", "POST", "{'longUrl': 'http://www.boligportal.dk" + link + "'}");
                    dynamic jsonResponse = JObject.Parse(googleResponse);
                    string shortUrl = jsonResponse.id;
                    string message = headline + " - Type: " + apartment.Type.Replace(" vær. lejlighed", "V") + " - Str.: " + apartment.Size + " m2 - Pris: " + apartment.Price + " - Link: " + shortUrl;
                    smsClient.DownloadString("http://www.cpsms.dk/sms/?username=Renomedia&recipient=4528688355&from=BoligBotten&password=KTPVQJ&message=" + message);

                    db.SubmitChanges();
                    Thread.Sleep(5000);
                }

                Console.WriteLine("Waiting 5 mins...");
                Thread.Sleep(300000);
;            }

            Console.ReadKey();
        }
    }
}
