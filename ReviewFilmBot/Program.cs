using Telegram.Bot;
using Telegram.Bot.Polling;
//using Telegram.Bot.Types.InputFiles;
using Telegram.Bot.Types.ReplyMarkups;
using Update = Telegram.Bot.Types.Update;
using ParseMode = Telegram.Bot.Types.Enums.ParseMode;
using InlineKeyboardMarkup = Telegram.Bot.Types.ReplyMarkups.InlineKeyboardMarkup;
using ForceReplyMarkup = Telegram.Bot.Types.ReplyMarkups.ForceReplyMarkup;
using UpdateType = Telegram.Bot.Types.Enums.UpdateType;
using Telegram.Bot.Types;
using Telegram.Bot.Exceptions;

class Program
{
    static TelegramBotClient bot;
    public static async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        Console.WriteLine(Newtonsoft.Json.JsonConvert.SerializeObject(update));

        var message = update.Message;
        if (update.Type == UpdateType.Message)
        {
            if (message.Text == null) return;
            if (message.Text.ToLower() == "путін")
            {
                await botClient.SendTextMessageAsync(message.Chat.Id, "<b>хуйло‼ СЛАВА УКРАЇНІ</b>", parseMode: ParseMode.Html);
                return;
            }
            if (message.Text.ToLower() == "/start")
            {
                var text = $"Hello, {message.From.FirstName}!\n\n" +
                    $"This is bot where you can <b>review</b> movies.\n\n" +
                    $"👉Type <u><i>/search movie title</i></u> to find movie\n" +
                    $"👉Type <u><i>/my_reviews</i></u> to get your reviews\n\n" +
                    $"👉To review a movie press <u><b>Write a review</b></u> button\n" +
                    $"on the inline keyboard when movie os found\n" +
                    $"👉To browse others' review on a movie use <u><b>Reviews</b></u> button\n" +
                    $"on the inline keyboard when movie os found\n\n" +
                    $"👉You can edit and delete your reviews by pressing\n" +
                    $"corresponding buttons";
                await botClient.SendTextMessageAsync(message.Chat.Id, text, parseMode: ParseMode.Html);
                return;
            }
            else if (message.Text.ToLower().StartsWith("/search"))
            {
                string searchQuery = message.Text.Substring(7).Replace(" ", "%20");

                Console.WriteLine("\n\n" + searchQuery + "\n\n");
                using (HttpClient client = new HttpClient())
                {
                    string apiUrl = $"https://review-a-film-botapi.azurewebsites.net/api/Movies/{searchQuery}"; // URL вашої API

                    // Виконання GET запиту та отримання відповіді
                    HttpResponseMessage response = await client.GetAsync(apiUrl);

                    // Перетворення відповіді в рядок
                    string responseString = await response.Content.ReadAsStringAsync();
                    var photoURL = responseString.Split().Last();
                    var responseWithNoPhotoURL = responseString.Remove(responseString.Length - photoURL.Length);
                    var buttons = new InlineKeyboardMarkup(new[]
                                    {
                                        new []
                                        {
                                            InlineKeyboardButton.WithCallbackData("Reviews"),
                                            InlineKeyboardButton.WithCallbackData("Write a review"),
                                        },
                                    });


                    try
                    {
                        using (var pic = await client.GetAsync(photoURL))
                        {
                            if (response.IsSuccessStatusCode)
                            {
                                using (var memoryStream = new MemoryStream())
                                {
                                    await pic.Content.CopyToAsync(memoryStream);
                                    memoryStream.Position = 0;
                                    //var inputFile = new InputOnlineFile(memoryStream);
                                    await botClient.SendPhotoAsync(
                                        message.Chat.Id,
                                        InputFile.FromUri(photoURL),
                                        caption:responseWithNoPhotoURL,
                                        parseMode: ParseMode.Html,
                                        replyMarkup: buttons);
                                }
                            }
                        }
                    }
                    catch (InvalidOperationException)
                    { }
                    catch (Exception)
                    {
                        await botClient.SendTextMessageAsync(
                        message.Chat.Id,
                        responseWithNoPhotoURL,
                        parseMode: ParseMode.Html,
                        replyMarkup: buttons);
                    }
                }
                return;
            }
            else if (message.Text.ToLower() == "/my_reviews")
            {
                var client = new HttpClient();
                int index = 0;
                string uri = $"https://review-a-film-botapi.azurewebsites.net/api/Review/GetMyReviewByIndex?tgUsername={message.From}&index={index}";

                var buttons = new InlineKeyboardMarkup(new[]
                {
                    new []
                    {
                         InlineKeyboardButton.WithCallbackData("⏪",$"{index-1}"),
                         InlineKeyboardButton.WithCallbackData("❌"),
                         InlineKeyboardButton.WithCallbackData("🖊"),
                         InlineKeyboardButton.WithCallbackData("⏩",$"{index+1}")
                    },
                    });

                var request = new HttpRequestMessage
                {
                    Method = HttpMethod.Get,
                    RequestUri = new Uri(uri),
                };
                using (var response = await client.SendAsync(request))
                {
                    response.EnsureSuccessStatusCode();
                    var body = await response.Content.ReadAsStringAsync();
                    Console.WriteLine(body);
                    await botClient.SendTextMessageAsync(message.Chat.Id, body,
                            parseMode: ParseMode.Html,
                            replyMarkup: buttons);
                }
            }
            if (message.ReplyToMessage != null && message.ReplyToMessage.From.Id == botClient.BotId)
            {
                if (update.Message.ReplyToMessage.Text.StartsWith("Write a review for "))
                {
                    var title = update.Message.ReplyToMessage.Text.Replace("Write a review for ", "").Replace(" ", "%20");
                    var client = new HttpClient();
                    var request = new HttpRequestMessage
                    {
                        Method = HttpMethod.Post,
                        RequestUri = new Uri($"https://review-a-film-botapi.azurewebsites.net/api/Review/PostReview?tgUsername={message.From}&movieId={title}&marking=-1&text={message.Text}"),
                    };
                    using (var response = await client.SendAsync(request))
                    {
                        response.EnsureSuccessStatusCode();
                        var body = await response.Content.ReadAsStringAsync();
                    }
                }
                else if (update.Message.ReplyToMessage.Text.StartsWith("Rewrite a review for "))
                {
                    var title = update.Message.ReplyToMessage.Text.Replace("Rewrite a review for ", "").Replace(" ", "%20");
                    var text = update.Message.Text.Replace(" ", "%20");

                    var uri = $"https://review-a-film-botapi.azurewebsites.net/api/Review/DeleteReview?tgUsername={update.Message.From}&movieId={title}";
                    HttpClient httpClient = new HttpClient();
                    HttpResponseMessage response = await httpClient.DeleteAsync(uri);

                    var client = new HttpClient();
                    var request = new HttpRequestMessage
                    {
                        Method = HttpMethod.Post,
                        RequestUri = new Uri($"https://review-a-film-botapi.azurewebsites.net/api/Review/PostReview?tgUsername={message.From}&movieId={title}&marking=-1&text={message.Text}"),
                    };
                    using (response = await client.SendAsync(request))
                    {
                        response.EnsureSuccessStatusCode();
                        var body = await response.Content.ReadAsStringAsync();
                    }
                }
                return;
            }
        }
        else if (update.Type == UpdateType.CallbackQuery)
        {
            ForceReplyMarkup forceReply = new ForceReplyMarkup();

            switch (update.CallbackQuery.Data)
            {
                case "Reviews":
                    int ind = 0;
                    var title = update.CallbackQuery.Message.Caption.Split("\n")[0] + ":";
                    Console.WriteLine(title);
                    var uri = $"https://review-a-film-botapi.azurewebsites.net/api/Review/reviews?Title={title}&index={ind}";
                    var client = new HttpClient();
                    var request = new HttpRequestMessage
                    {
                        Method = HttpMethod.Get,
                        RequestUri = new Uri(uri),
                    };
                    using (var responseReview = await client.SendAsync(request))
                    {
                        var buttons = new InlineKeyboardMarkup(new[]
                    {
                            new []
                        {
                         InlineKeyboardButton.WithCallbackData("⏪",$" ,{ind-1}"),
                         InlineKeyboardButton.WithCallbackData("⏩",$" ,{ind+1}")
                        },
                    });
                        responseReview.EnsureSuccessStatusCode();
                        var body = await responseReview.Content.ReadAsStringAsync();
                        await botClient.SendTextMessageAsync(update.CallbackQuery.Message.Chat.Id,
                        body, parseMode: ParseMode.Html, replyMarkup: buttons);
                        await botClient.AnswerCallbackQueryAsync(update.CallbackQuery.Id);
                    }
                    return;
                    break;
                case "Write a review":
                    await botClient.SendTextMessageAsync(update.CallbackQuery.Message.Chat.Id, $"Write a review for {update.CallbackQuery.Message.Caption.Split("\n")[0]}:", replyMarkup: forceReply);
                    await botClient.AnswerCallbackQueryAsync(update.CallbackQuery.Id);
                    return;
                case "❌":
                    title = update.CallbackQuery.Message.Text.Split(" :")[0] + " :";
                    Console.WriteLine(title);

                    uri = $"https://review-a-film-botapi.azurewebsites.net/api/Review/DeleteReview?tgUsername={update.CallbackQuery.From}&movieId={title}";
                    HttpClient httpClient = new HttpClient();
                    HttpResponseMessage response = await httpClient.DeleteAsync(uri);
                    ind = 0;
                    uri = $"https://review-a-film-botapi.azurewebsites.net/api/Review/GetMyReviewByIndex?tgUsername={update.CallbackQuery.From}&index={ind}";
                    client = new HttpClient();
                    request = new HttpRequestMessage
                    {
                        Method = HttpMethod.Get,
                        RequestUri = new Uri(uri),
                    };
                    using (response = await client.SendAsync(request))
                    {
                        var buttons = new InlineKeyboardMarkup(new[]
                    {
                            new []
                        {
                         InlineKeyboardButton.WithCallbackData("⏪",$"{ind-1}"),
                         InlineKeyboardButton.WithCallbackData("❌"),
                         InlineKeyboardButton.WithCallbackData("🖊"),
                         InlineKeyboardButton.WithCallbackData("⏩",$"{ind+1}")
                        },
                    });
                        response.EnsureSuccessStatusCode();
                        var body = await response.Content.ReadAsStringAsync();
                        await botClient.EditMessageTextAsync(update.CallbackQuery.Message.Chat.Id, update.CallbackQuery.Message.MessageId,
                        body, ParseMode.Html, replyMarkup: buttons);
                        await botClient.AnswerCallbackQueryAsync(update.CallbackQuery.Id);
                    }
                    return;
                case "🖊":
                    ForceReplyMarkup forceReplyMarkup = new ForceReplyMarkup();
                    title = update.CallbackQuery.Message.Text.Split(" :")[0] + " :";
                    await botClient.SendTextMessageAsync(update.CallbackQuery.Message.Chat.Id, $"Rewrite a review for {title}",
                        replyMarkup: forceReplyMarkup);
                    await botClient.AnswerCallbackQueryAsync(update.CallbackQuery.Id);
                    return;
            }
            if (int.TryParse(update.CallbackQuery.Data, out int index))
            {
                var client = new HttpClient();
                var uri = $"https://review-a-film-botapi.azurewebsites.net/api/Review/GetMyReviewByIndex?tgUsername={update.CallbackQuery.From}&index={index}";

                var request = new HttpRequestMessage
                {
                    Method = HttpMethod.Get,
                    RequestUri = new Uri(uri),
                };
                using (var response = await client.SendAsync(request))
                {
                    response.EnsureSuccessStatusCode();
                    var body = await response.Content.ReadAsStringAsync();
                    var buttons = new InlineKeyboardMarkup(new[]
{
                            new []
                        {
                         InlineKeyboardButton.WithCallbackData("⏪",$"{index+1}"),
                         InlineKeyboardButton.WithCallbackData("❌"),
                         InlineKeyboardButton.WithCallbackData("🖊"),
                         InlineKeyboardButton.WithCallbackData("⏩",$"{index-1}")
                        },
                    });
                    Console.WriteLine(body);
                    await botClient.EditMessageTextAsync(update.CallbackQuery.Message.Chat.Id, update.CallbackQuery.Message.MessageId,
                        body, ParseMode.Html, replyMarkup: buttons);
                    await botClient.AnswerCallbackQueryAsync(update.CallbackQuery.Id);
                }
                return;
            }
            else if (int.TryParse(update.CallbackQuery.Data.Split(",").Last(), out index))
            {
                var title = update.CallbackQuery.Message.Text.Split(" :").First() + " :";
                Console.WriteLine(title);
                var client = new HttpClient();
                var uri = $"https://review-a-film-botapi.azurewebsites.net/api/Review/reviews?Title={title}&index={index}";

                var request = new HttpRequestMessage
                {
                    Method = HttpMethod.Get,
                    RequestUri = new Uri(uri),
                };
                using (var response = await client.SendAsync(request))
                {
                    response.EnsureSuccessStatusCode();
                    var body = await response.Content.ReadAsStringAsync();
                    var buttons = new InlineKeyboardMarkup(new[]
{
                            new []
                        {
                         InlineKeyboardButton.WithCallbackData("⏪",$" ,{index+1}"),
                         InlineKeyboardButton.WithCallbackData("⏩",$" ,{index-1}")
                        },
                    });
                    Console.WriteLine(body);
                    await botClient.EditMessageTextAsync(update.CallbackQuery.Message.Chat.Id, update.CallbackQuery.Message.MessageId,
                        body, ParseMode.Html, replyMarkup: buttons);
                    await botClient.AnswerCallbackQueryAsync(update.CallbackQuery.Id);
                }
                return;
            }

            return;
        }
    }
    public static Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
    {
        var ErrorMessage = exception switch
        {
            ApiRequestException apiRequestException
                => $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
            _ => exception.ToString()
        };

        Console.WriteLine(ErrorMessage);
        return Task.CompletedTask;
    }

    private static void Main(string[] args)
    {
        bot = new TelegramBotClient("6095290532:AAG7Ask50jYtMXkR3mOkTdBxvHKAjZs9rWM");
        var cts = new CancellationTokenSource();
        var cancellationToken = cts.Token;
        var receiverOptions = new ReceiverOptions
        {
            AllowedUpdates = new[] {UpdateType.Message,UpdateType.CallbackQuery }, 
        };

        bot.StartReceiving(
            HandleUpdateAsync,
            HandleErrorAsync,
            receiverOptions,
            cancellationToken
        );
        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container.
        builder.Services.AddRazorPages();

        var app = builder.Build();

        // Configure the HTTP request pipeline.
        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Error");
            // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
            app.UseHsts();
        }

        app.UseHttpsRedirection();
        app.UseStaticFiles();

        app.UseRouting();

        app.UseAuthorization();

        app.MapRazorPages();

        app.Run();
    }
}