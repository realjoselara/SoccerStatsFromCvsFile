﻿using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using System.Net;
using System.Text;

namespace SoccerStats
{
    class Program
    {
        static void Main(string[] args)
        {
            string currentDirectory = Directory.GetCurrentDirectory();
            DirectoryInfo directory = new DirectoryInfo(currentDirectory);
            var fileName = Path.Combine(directory.FullName, "SoccerGameResults.csv");

            var fileContent = ReadSoccerResults(fileName);

            Console.WriteLine("Printing from Local File");
            foreach (var content in fileContent)
            {
                Console.WriteLine("{0} - Game Played: {1} - Team: {2}",content.GameDate, content.HomeORAway, content.TeamName);
                Console.WriteLine();
            }

            fileName = Path.Combine(directory.FullName, "players.json");
            var players = DeserializePlayers(fileName);
            var toptenplayers = GetTopTenPlayers(players);

            Console.WriteLine("Printing from Bing's News API");
            foreach(var player in toptenplayers)
            {
                List<NewsResults> newsResults = GetNewsForPlayer(string.Format("{0} {1}", player.FirstName, player.LastName));
                SentimentResponse sentimentResponse = GetSentimentResponse(newsResults);

                foreach(var sentiment in sentimentResponse.Sentiments)
                {
                    foreach (var result in newsResults)
                    {
                        if(result.Headline == sentiment.Id)
                        {
                            double score;
                            if(double.TryParse(sentiment.Score, out score))
                            {
                                result.SentimentScore = score;
                            }
                            break;
                        }
                        Console.WriteLine(string.Format("Sentiment Score: {0:P}, Date: {1:f}, Headline: {2}, Summary: {3}\r\n",result.SentimentScore, result.DatePublished, result.Headline, result.Summary));

                    }
                    
                }
               
               

            }
            fileName = Path.Combine(directory.FullName, "topten.json");
            SerializePlayersToFile(toptenplayers, fileName);

        }
        public static string ReadFile(string fileName){
            using(var reader = new StreamReader(fileName)){

                return reader.ReadToEnd();
            }

        }

        // Get the data from file and place it in a List of Game Results
        // return List
        public static List<GameResult> ReadSoccerResults(string fileName)
        {
            var soccerResults = new List<GameResult>();
            using (var reader = new StreamReader(fileName))
            {
                string line = "";
                reader.ReadLine();
                while ((line = reader.ReadLine()) != null)
                {
                    var gameResult = new GameResult();
                    string[] values = line.Split(',');

                    DateTime gameDate;
                    if (DateTime.TryParse(values[0], out gameDate))
                    {
                        gameResult.GameDate = gameDate;
                    }
                    gameResult.TeamName = values[1];
                    HomeOrAway homeOrAway;
                    if (Enum.TryParse(values[2], out homeOrAway))
                    {
                        gameResult.HomeORAway = homeOrAway;
                    }
                    int parseInt;
                    if (int.TryParse(values[3], out parseInt))
                    {
                        gameResult.Goals = parseInt;
                    }
                    if (int.TryParse(values[4], out parseInt))
                    {
                        gameResult.GoalAttempts = parseInt;
                    }
                    if (int.TryParse(values[5], out parseInt))
                    {
                        gameResult.ShotsOnGoal = parseInt;
                    }
                    if (int.TryParse(values[6], out parseInt))
                    {
                        gameResult.ShotsOffGoal = parseInt;
                    }

                    double possessionPercent;
                    if (double.TryParse(values[7], out possessionPercent))
                    {
                        gameResult.PossessionPercent = possessionPercent;
                    }
                    soccerResults.Add(gameResult);
                }
            }
            return soccerResults;
        }

        public static List<Player> DeserializePlayers(string fileName)
        {
            var players = new List<Player>();
            var serializer = new JsonSerializer();
            using (var reader = new StreamReader(fileName))
            using (var jsonReader = new JsonTextReader(reader))
            {
                players = serializer.Deserialize<List<Player>>(jsonReader);
            }

            return players;
        }

        public static List<Player> GetTopTenPlayers(List<Player> players)
        {
            var topTenPlayers = new List<Player>();
            players.Sort(new PlayerComparer());
            int counter = 0;
            foreach (var player in players)
            {
                topTenPlayers.Add(player);
                counter++;
                if (counter == 10)
                    break;
            }
            return topTenPlayers;
        }

        public static void SerializePlayersToFile(List<Player> players, string fileName)
        {
            var serializer = new JsonSerializer();
            using (var writer = new StreamWriter(fileName))
            using (var jsonWriter = new JsonTextWriter(writer))
            {
                serializer.Serialize(jsonWriter, players);
            }
        }

        public static string GetGoogleHomePage(){

            var webClient = new WebClient();
            byte[] GoogleHome = webClient.DownloadData("http://www.google.com");

            using(var stream = new MemoryStream(GoogleHome))
            using (var reader = new StreamReader(stream)){
                return reader.ReadToEnd();
            }
        }

        // This function makes a API call to the bing news api to get 
        // news for players
        public static List<NewsResults> GetNewsForPlayer(string playerName)
        {
            var results = new List<NewsResults>();

            var webClient = new WebClient();
            webClient.Headers.Add("Ocp-Apim-Subscription-Key", "95e29aec68e84cc6a5af3d1611c4386b");
            byte[] searchResults = webClient.DownloadData(string.Format("https://api.cognitive.microsoft.com/bing/v7.0/news/search?q={0}&mkt=en-us", playerName));

            var serializer = new JsonSerializer();
            using (var stream = new MemoryStream(searchResults))
            using (var reader = new StreamReader(stream))
            using(var jsonReader = new JsonTextReader(reader))
            {
                results = serializer.Deserialize<NewsSearch>(jsonReader).NewsResults;
            }
            return results;
        }

        public static SentimentResponse GetSentimentResponse(List<NewsResults> newsResults) {

            var sentimentResponse = new SentimentResponse();
            var sentimentRequest = new SentimentRequest();

            sentimentRequest.Documents = new List<Document>();

            foreach(var result in newsResults)
            {
                sentimentRequest.Documents.Add(new Document{Id = result.Headline, Text = result.Summary});
            }

            var webClient = new WebClient();
            webClient.Headers.Add("Ocp-Apim-Subscription-Key", "1c6dcbb92bdd49329541c96c25ddd00b");
            webClient.Headers.Add(HttpRequestHeader.Accept, "application/json");
            webClient.Headers.Add(HttpRequestHeader.ContentType, "application/json");
            string requestJson = JsonConvert.SerializeObject(sentimentRequest);
            byte[] requestBytes = Encoding.UTF8.GetBytes(requestJson);
            byte[] response = webClient.UploadData("https://westcentralus.api.cognitive.microsoft.com/text/analytics/v2.0/sentiment", requestBytes);
            string sentiments = Encoding.UTF8.GetString(response);
            sentimentResponse = JsonConvert.DeserializeObject<SentimentResponse>(sentiments);

            return sentimentResponse;
        }

    }
}