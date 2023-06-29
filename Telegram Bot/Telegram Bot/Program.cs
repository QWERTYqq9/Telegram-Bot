using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using MongoDB.Driver;
using MongoDB.Bson;

namespace TelegramBotExperiments
{
    public class GenreInfo
    {
        public string Name { get; set; }
    }

    public class GameIdInfo
    {
        public string Id { get; set; }
    }

    public class GameDetails
    {
        public string Name { get; set; }
        public bool IsFree { get; set; }
        public string About { get; set; }
        public string HeaderImage { get; set; }
        public string Website { get; set; }
        public string[] Developers { get; set; }
        public string[] Publishers { get; set; }
    }

    class Program
    {
        private static ITelegramBotClient botClient;
        private static HttpClient _httpClient = new HttpClient();
        private static IMongoCollection<GenreInfo> genreCollection;

        private static Dictionary<long, string> userStates = new Dictionary<long, string>();

        static async Task Main(string[] args)
        {
            string botToken = "5687028607:AAE-EjCR_o9kK818LvQ_huB7PslLzMvpTgQ";
            botClient = new TelegramBotClient(botToken);

            Console.WriteLine($"������� ��� {botClient.GetMeAsync().Result.FirstName}");

            var cts = new CancellationTokenSource();
            var cancellationToken = cts.Token;

            var receiverOptions = new ReceiverOptions
            {
                AllowedUpdates = Array.Empty<UpdateType>() // ��������� ���� ����� ����������
            };

            botClient.StartReceiving(HandleUpdateAsync, HandleErrorAsync, receiverOptions, cancellationToken);

            // ����������� � MongoDB Atlas
            var mongoClient = new MongoClient("mongodb+srv://superkill2004:qz5UPbz9P50ziMtr@cluster0.qotaghi.mongodb.net/?retryWrites=true&w=majority");
            var database = mongoClient.GetDatabase("AllGenres");
            genreCollection = database.GetCollection<GenreInfo>("Genres");

            Console.WriteLine("������� ����� ������� ��� ������.");
            Console.ReadLine();
            cts.Cancel();
        }

        private static async Task<List<GenreInfo>> GetNewGenres()
        {
            var genres = await genreCollection.Find(_ => true).ToListAsync();
            return genres;
        }

        private static async Task HandleGetNewGenresCommand(ITelegramBotClient botClient, Message message)
        {
            try
            {
                // �������� ����� ����� �� ���� ������
                var genres = await GetNewGenres();

                // ������������ ����� ������������
                var reply = "������ ����� ������:\n";
                foreach (var genre in genres)
                {
                    reply += $"- {genre.Name}\n";
                }

                // ��������� ����� ������������
                await botClient.SendTextMessageAsync(message.Chat.Id, reply);
            }
            catch (Exception ex)
            {
                // ��������� ����������
                await botClient.SendTextMessageAsync(message.Chat.Id, "��������� ������ ��� ���������� ������� � ���� ������.");
            }
        }

        private static async Task HandleGetGenresCommand(ITelegramBotClient botClient, Message message)
        {
            try
            {
                // ��������� GET ������ � ����� API � �������� ������ ������
                var response = await _httpClient.GetAsync("https://localhost:7083/genres");
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var genreInfos = JsonConvert.DeserializeObject<List<GenreInfo>>(content);

                    // ������������ ����� ������������
                    var reply = "������� ����, ��� ���� �������� �� ������ ������:\n";
                    foreach (var genre in genreInfos)
                    {
                        reply += $"- {genre.Name}\n";
                    }

                    // ��������� ����� ������������
                    await botClient.SendTextMessageAsync(message.Chat.Id, reply);
                }
                else
                {
                    // ��������� ������ ��� ������ ����� API
                    await botClient.SendTextMessageAsync(message.Chat.Id, "��������� ������ ��� ��������� ������ ������.");
                }
            }
            catch (Exception ex)
            {
                // ��������� ����������
                await botClient.SendTextMessageAsync(message.Chat.Id, "��������� ������ ��� ���������� ������� � API.");
            }
        }

        private static async Task HandleGenreSelection(ITelegramBotClient botClient, Message message, string genre)
        {
            try
            {
                // ��������� GET ������ � ����� API � �������� ���� �� ���������� �����
                var url = $"https://localhost:7083/games/{Uri.EscapeDataString(genre)}";
                var response = await _httpClient.GetAsync(url);

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var games = JsonConvert.DeserializeObject<List<GameIdInfo>>(content);

                    // ������������ ����� ������������
                    var reply = $"������ ��� �� ���������� ����� \"{genre}\":\n";
                    foreach (var game in games)
                    {
                        reply += $"- {game.Id}\n";
                    }

                    // ��������� ����� ������������
                    await botClient.SendTextMessageAsync(message.Chat.Id, reply);

                }
                else
                {
                    // ��������� ������ ��� ������ ����� API
                    await botClient.SendTextMessageAsync(message.Chat.Id, "��������� ������ ��� ��������� ������ ���.");
                }
            }
            catch (Exception ex)
            {
                // ��������� ����������
                await botClient.SendTextMessageAsync(message.Chat.Id, "��������� ������ ��� ���������� ������� � API.");
            }
        }

        private static async Task HandleGameDetails(ITelegramBotClient botClient, Message message, string gameId)
        {
            try
            {
                // ��������� ������ � ����� API � �������� ���������� � ��������� ����
                var url = $"https://localhost:7083/game/{Uri.EscapeDataString(gameId)}";
                var response = await _httpClient.GetAsync(url);

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var gameDetails = JsonConvert.DeserializeObject<GameDetails>(content);

                    // ������������ ����� ������������
                    var reply = $"���������� �� ���� \"{gameDetails.Name}\":\n" +
                                $"- ��������: {gameDetails.About}\n" +
                                $"- ������������: {string.Join(", ", gameDetails.Developers)}\n" +
                                $"- ��������: {string.Join(", ", gameDetails.Publishers)}\n" +
                                $"- ����������: {(gameDetails.IsFree ? "��" : "���")}\n" +
                                $"- ������ �� ����: {gameDetails.Website}";

                    // ��������� ����� ������������
                    await botClient.SendTextMessageAsync(message.Chat.Id, reply);
                }
                else
                {
                    // ��������� ������ ��� ������ ����� API
                    await botClient.SendTextMessageAsync(message.Chat.Id, "��������� ������ ��� ��������� ���������� �� ����.");
                }
            }
            catch (Exception ex)
            {
                // ��������� ����������
                await botClient.SendTextMessageAsync(message.Chat.Id, "��������� ������ ��� ���������� ������� � API.");
            }
        }

        private static async Task HandleRemoveGenreCommand(ITelegramBotClient botClient, Message message, string genreName)
        {
            try
            {
                // ������� ���� �� ���� ������
                var deleteResult = await genreCollection.DeleteOneAsync(genre => genre.Name.ToLower() == genreName.ToLower());

                if (deleteResult.DeletedCount > 0)
                {
                    await botClient.SendTextMessageAsync(message.Chat.Id, $"���� \"{genreName}\" ������� ������.");
                }
                else
                {
                    await botClient.SendTextMessageAsync(message.Chat.Id, $"���� \"{genreName}\" �� ������.");
                }
            }
            catch (Exception ex)
            {
                // ��������� ����������
                await botClient.SendTextMessageAsync(message.Chat.Id, "��������� ������ ��� �������� �����.");
            }
        }

        private static async Task HandleAddGenreCommand(ITelegramBotClient botClient, Message message, string genreName)
        {
            try
            {
                // ���������, ���������� �� ��� ���� � ���� ������
                var existingGenre = await genreCollection.Find(genre => genre.Name.ToLower() == genreName.ToLower()).FirstOrDefaultAsync();

                if (existingGenre == null)
                {
                    // ������� ����� ������ GenreInfo
                    var genreInfo = new GenreInfo { Name = genreName };

                    // ��������� ���� � ���� ������
                    await genreCollection.InsertOneAsync(genreInfo);

                    await botClient.SendTextMessageAsync(message.Chat.Id, $"���� \"{genreName}\" ������� ��������.");
                }
                else
                {
                    await botClient.SendTextMessageAsync(message.Chat.Id, $"���� \"{genreName}\" ��� ���������� � ���� ������.");
                }
            }
            catch (Exception ex)
            {
                // ��������� ����������
                await botClient.SendTextMessageAsync(message.Chat.Id, "��������� ������ ��� ���������� �����.");
            }
        }

        private static async Task HandleHelpCommand(ITelegramBotClient botClient, Message message)
        {
            var reply = "��������� �������:\n" +
                        "/start - ������ ������������� ����\n" +
                        "/get_genres - �������� ������ ��������� ������\n" +
                        "/get_new_genres - �������� ������ ����� ������\n" +
                        "/remove_unwanted_genre - ������� ������������� ����\n" +
                        "/add_unwanted_genre - �������� ������������� ����\n" +
                        "������� �������� ����� ��� ID ���� ��� ��������� ����������";

            await botClient.SendTextMessageAsync(message.Chat.Id, reply);
        }

        static async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            Console.WriteLine(Newtonsoft.Json.JsonConvert.SerializeObject(update));

            if (update.Type == UpdateType.Message)
            {
                var message = update.Message;

                switch (message.Text.ToLower())
                {
                    case "/start":
                        await botClient.SendTextMessageAsync(message.Chat.Id, "����� ����������! ���� ��� ������� ������� ������������ ����, � ������� �� ����������� �������� ��������.");
                        break;
                    case "/get_genres":
                        await HandleGetGenresCommand(botClient, message);
                        break;
                    case "/get_new_genres":
                        await HandleGetNewGenresCommand(botClient, message);
                        break;
                    case "/remove_unwanted_genre":
                        // ������������� ��������� "remove_genre" ��� �������� ������������
                        userStates[message.Chat.Id] = "remove_genre";
                        await botClient.SendTextMessageAsync(message.Chat.Id, "������� ����, ������� �� ������ �������:");
                        break;
                    case "/add_unwanted_genre":
                        // ������������� ��������� "add_genre" ��� �������� ������������
                        userStates[message.Chat.Id] = "add_genre";
                        await botClient.SendTextMessageAsync(message.Chat.Id, "������� ����, ������� �� ������ ��������:");
                        break;
                    case "/help":
                        await HandleHelpCommand(botClient, message);
                        break;
                    default:
                        // ��������� ��������� ������������
                        if (userStates.TryGetValue(message.Chat.Id, out var state))
                        {
                            if (state == "remove_genre")
                            {
                                // �������� ���� �� ������ ������������
                                var name = message.Text;

                                // ������������ �������� �����
                                await HandleRemoveGenreCommand(botClient, message, name);

                                // ������� ��������� "remove_genre" ��� �������� ������������
                                userStates.Remove(message.Chat.Id);
                            }
                            else if (state == "add_genre")
                            {
                                // �������� ���� �� ������ ������������
                                var name = message.Text;

                                // ������������ ���������� �����
                                await HandleAddGenreCommand(botClient, message, name);

                                // ������� ��������� "add_genre" ��� �������� ������������
                                userStates.Remove(message.Chat.Id);
                            }
                            // ������ �������� ��������� ��� ������ ��������
                        }
                        else
                        {
                            // ���������, �������� �� ��������� ������ ����� (id ����)
                            var input = message.Text.Trim();
                            if (!string.IsNullOrEmpty(input))
                            {
                                bool isNumeric = int.TryParse(input, out _);
                                if (isNumeric)
                                {
                                    await HandleGameDetails(botClient, message, input);
                                }
                                else
                                {
                                    await HandleGenreSelection(botClient, message, input);
                                }
                            }
                            else
                            {
                                // ��������� ����������� �������
                                await botClient.SendTextMessageAsync(message.Chat.Id, "����������� �������. ���������� ������ �������.");
                            }
                        }
                        break;
                }
            }
        }

        static async Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
        {
            Console.WriteLine(Newtonsoft.Json.JsonConvert.SerializeObject(exception));
        }
    }
}
