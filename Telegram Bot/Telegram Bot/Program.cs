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

            Console.WriteLine($"Запущен бот {botClient.GetMeAsync().Result.FirstName}");

            var cts = new CancellationTokenSource();
            var cancellationToken = cts.Token;

            var receiverOptions = new ReceiverOptions
            {
                AllowedUpdates = Array.Empty<UpdateType>() // Обработка всех типов обновлений
            };

            botClient.StartReceiving(HandleUpdateAsync, HandleErrorAsync, receiverOptions, cancellationToken);

            // Подключение к MongoDB Atlas
            var mongoClient = new MongoClient("mongodb+srv://superkill2004:qz5UPbz9P50ziMtr@cluster0.qotaghi.mongodb.net/?retryWrites=true&w=majority");
            var database = mongoClient.GetDatabase("AllGenres");
            genreCollection = database.GetCollection<GenreInfo>("Genres");

            Console.WriteLine("Нажмите любую клавишу для выхода.");
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
                // Получить новые жанры из базы данных
                var genres = await GetNewGenres();

                // Сформировать ответ пользователю
                var reply = "Список новых жанров:\n";
                foreach (var genre in genres)
                {
                    reply += $"- {genre.Name}\n";
                }

                // Отправить ответ пользователю
                await botClient.SendTextMessageAsync(message.Chat.Id, reply);
            }
            catch (Exception ex)
            {
                // Обработка исключения
                await botClient.SendTextMessageAsync(message.Chat.Id, "Произошла ошибка при выполнении запроса к базе данных.");
            }
        }

        private static async Task HandleGetGenresCommand(ITelegramBotClient botClient, Message message)
        {
            try
            {
                // Выполнить GET запрос к вашей API и получить список жанров
                var response = await _httpClient.GetAsync("https://localhost:7083/genres");
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var genreInfos = JsonConvert.DeserializeObject<List<GenreInfo>>(content);

                    // Сформировать ответ пользователю
                    var reply = "Введите жанр, про игры которого вы хотите узнать:\n";
                    foreach (var genre in genreInfos)
                    {
                        reply += $"- {genre.Name}\n";
                    }

                    // Отправить ответ пользователю
                    await botClient.SendTextMessageAsync(message.Chat.Id, reply);
                }
                else
                {
                    // Обработка ошибки при вызове вашей API
                    await botClient.SendTextMessageAsync(message.Chat.Id, "Произошла ошибка при получении списка жанров.");
                }
            }
            catch (Exception ex)
            {
                // Обработка исключения
                await botClient.SendTextMessageAsync(message.Chat.Id, "Произошла ошибка при выполнении запроса к API.");
            }
        }

        private static async Task HandleGenreSelection(ITelegramBotClient botClient, Message message, string genre)
        {
            try
            {
                // Выполнить GET запрос к вашей API и получить игры по выбранному жанру
                var url = $"https://localhost:7083/games/{Uri.EscapeDataString(genre)}";
                var response = await _httpClient.GetAsync(url);

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var games = JsonConvert.DeserializeObject<List<GameIdInfo>>(content);

                    // Сформировать ответ пользователю
                    var reply = $"Список игр по выбранному жанру \"{genre}\":\n";
                    foreach (var game in games)
                    {
                        reply += $"- {game.Id}\n";
                    }

                    // Отправить ответ пользователю
                    await botClient.SendTextMessageAsync(message.Chat.Id, reply);

                }
                else
                {
                    // Обработка ошибки при вызове вашей API
                    await botClient.SendTextMessageAsync(message.Chat.Id, "Произошла ошибка при получении списка игр.");
                }
            }
            catch (Exception ex)
            {
                // Обработка исключения
                await botClient.SendTextMessageAsync(message.Chat.Id, "Произошла ошибка при выполнении запроса к API.");
            }
        }

        private static async Task HandleGameDetails(ITelegramBotClient botClient, Message message, string gameId)
        {
            try
            {
                // Выполнить запрос к вашей API и получить информацию о выбранной игре
                var url = $"https://localhost:7083/game/{Uri.EscapeDataString(gameId)}";
                var response = await _httpClient.GetAsync(url);

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var gameDetails = JsonConvert.DeserializeObject<GameDetails>(content);

                    // Сформировать ответ пользователю
                    var reply = $"Информация об игре \"{gameDetails.Name}\":\n" +
                                $"- Описание: {gameDetails.About}\n" +
                                $"- Разработчики: {string.Join(", ", gameDetails.Developers)}\n" +
                                $"- Издатели: {string.Join(", ", gameDetails.Publishers)}\n" +
                                $"- Бесплатная: {(gameDetails.IsFree ? "Да" : "Нет")}\n" +
                                $"- Ссылка на сайт: {gameDetails.Website}";

                    // Отправить ответ пользователю
                    await botClient.SendTextMessageAsync(message.Chat.Id, reply);
                }
                else
                {
                    // Обработка ошибки при вызове вашей API
                    await botClient.SendTextMessageAsync(message.Chat.Id, "Произошла ошибка при получении информации об игре.");
                }
            }
            catch (Exception ex)
            {
                // Обработка исключения
                await botClient.SendTextMessageAsync(message.Chat.Id, "Произошла ошибка при выполнении запроса к API.");
            }
        }

        private static async Task HandleRemoveGenreCommand(ITelegramBotClient botClient, Message message, string genreName)
        {
            try
            {
                // Удалить жанр из базы данных
                var deleteResult = await genreCollection.DeleteOneAsync(genre => genre.Name.ToLower() == genreName.ToLower());

                if (deleteResult.DeletedCount > 0)
                {
                    await botClient.SendTextMessageAsync(message.Chat.Id, $"Жанр \"{genreName}\" успешно удален.");
                }
                else
                {
                    await botClient.SendTextMessageAsync(message.Chat.Id, $"Жанр \"{genreName}\" не найден.");
                }
            }
            catch (Exception ex)
            {
                // Обработка исключения
                await botClient.SendTextMessageAsync(message.Chat.Id, "Произошла ошибка при удалении жанра.");
            }
        }

        private static async Task HandleAddGenreCommand(ITelegramBotClient botClient, Message message, string genreName)
        {
            try
            {
                // Проверяем, существует ли уже жанр в базе данных
                var existingGenre = await genreCollection.Find(genre => genre.Name.ToLower() == genreName.ToLower()).FirstOrDefaultAsync();

                if (existingGenre == null)
                {
                    // Создаем новый объект GenreInfo
                    var genreInfo = new GenreInfo { Name = genreName };

                    // Вставляем жанр в базу данных
                    await genreCollection.InsertOneAsync(genreInfo);

                    await botClient.SendTextMessageAsync(message.Chat.Id, $"Жанр \"{genreName}\" успешно добавлен.");
                }
                else
                {
                    await botClient.SendTextMessageAsync(message.Chat.Id, $"Жанр \"{genreName}\" уже существует в базе данных.");
                }
            }
            catch (Exception ex)
            {
                // Обработка исключения
                await botClient.SendTextMessageAsync(message.Chat.Id, "Произошла ошибка при добавлении жанра.");
            }
        }

        private static async Task HandleHelpCommand(ITelegramBotClient botClient, Message message)
        {
            var reply = "Доступные команды:\n" +
                        "/start - Начать использование бота\n" +
                        "/get_genres - Получить список доступных жанров\n" +
                        "/get_new_genres - Получить список новых жанров\n" +
                        "/remove_unwanted_genre - Удалить нежелательный жанр\n" +
                        "/add_unwanted_genre - Добавить нежелательный жанр\n" +
                        "Введите название жанра или ID игры для получения информации";

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
                        await botClient.SendTextMessageAsync(message.Chat.Id, "Добро пожаловать! Этот бот поможет выбрать компьютерную игру, в которую ты обязательно захочешь поиграть.");
                        break;
                    case "/get_genres":
                        await HandleGetGenresCommand(botClient, message);
                        break;
                    case "/get_new_genres":
                        await HandleGetNewGenresCommand(botClient, message);
                        break;
                    case "/remove_unwanted_genre":
                        // Устанавливаем состояние "remove_genre" для текущего пользователя
                        userStates[message.Chat.Id] = "remove_genre";
                        await botClient.SendTextMessageAsync(message.Chat.Id, "Укажите жанр, который вы хотите удалить:");
                        break;
                    case "/add_unwanted_genre":
                        // Устанавливаем состояние "add_genre" для текущего пользователя
                        userStates[message.Chat.Id] = "add_genre";
                        await botClient.SendTextMessageAsync(message.Chat.Id, "Укажите жанр, который вы хотите добавить:");
                        break;
                    case "/help":
                        await HandleHelpCommand(botClient, message);
                        break;
                    default:
                        // Проверяем состояние пользователя
                        if (userStates.TryGetValue(message.Chat.Id, out var state))
                        {
                            if (state == "remove_genre")
                            {
                                // Получаем жанр из ответа пользователя
                                var name = message.Text;

                                // Обрабатываем удаление жанра
                                await HandleRemoveGenreCommand(botClient, message, name);

                                // Удаляем состояние "remove_genre" для текущего пользователя
                                userStates.Remove(message.Chat.Id);
                            }
                            else if (state == "add_genre")
                            {
                                // Получаем жанр из ответа пользователя
                                var name = message.Text;

                                // Обрабатываем добавление жанра
                                await HandleAddGenreCommand(botClient, message, name);

                                // Удаляем состояние "add_genre" для текущего пользователя
                                userStates.Remove(message.Chat.Id);
                            }
                            // Другие проверки состояний для разных операций
                        }
                        else
                        {
                            // Проверить, содержит ли сообщение только цифры (id игры)
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
                                // Обработка неизвестной команды
                                await botClient.SendTextMessageAsync(message.Chat.Id, "Неизвестная команда. Попробуйте другую команду.");
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
