using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;

namespace SimpleChatbot.Controllers
{
    [Authorize]
    public class ChatController : Controller
    {
        private readonly string cs = ConfigurationManager.ConnectionStrings["DefaultConnection"].ConnectionString;

        [HttpGet]
        public ActionResult Index()
        {
            return View();
        }

        [HttpGet]
        public async Task<JsonResult> GetUserQuota()
        {
            string userIdStr = Session["UserId"]?.ToString();
            if (string.IsNullOrEmpty(userIdStr))
                return Json(new { success = false, error = "Session expired" }, JsonRequestBehavior.AllowGet);

            int userId = Convert.ToInt32(userIdStr);

            try
            {
                using (SqlConnection con = new SqlConnection(cs))
                {
                    string query = "SELECT Quota, UsedCount FROM Users WHERE Id=@id";
                    using (SqlCommand cmd = new SqlCommand(query, con))
                    {
                        cmd.Parameters.AddWithValue("@id", userId);
                        await con.OpenAsync();
                        using (SqlDataReader r = await cmd.ExecuteReaderAsync())
                        {
                            if (await r.ReadAsync())
                            {
                                int quota = Convert.ToInt32(r["Quota"]);
                                int used = Convert.ToInt32(r["UsedCount"]);
                                int remaining = quota - used;
                                double percentage = quota > 0 ? ((double)remaining / quota) * 100 : 0;

                                return Json(new
                                {
                                    success = true,
                                    quota = quota,
                                    used = used,
                                    remaining = remaining,
                                    percentage = Math.Round(percentage, 1)
                                }, JsonRequestBehavior.AllowGet);
                            }
                        }
                    }
                }
                return Json(new { success = false, error = "User not found" }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        [HttpGet]
        public async Task<JsonResult> GetHistory()
        {
            var historyData = new List<object>();
            string userIdStr = Session["UserId"]?.ToString();

            if (string.IsNullOrEmpty(userIdStr))
                return Json(new { success = false, error = "Session expired" }, JsonRequestBehavior.AllowGet);

            int userId = Convert.ToInt32(userIdStr);

            try
            {
                using (SqlConnection con = new SqlConnection(cs))
                {
                    string query = "SELECT TOP 15 Id, Question FROM ChatHistory WHERE UserId = @u ORDER BY Id DESC";
                    using (SqlCommand cmd = new SqlCommand(query, con))
                    {
                        cmd.Parameters.AddWithValue("@u", userId);
                        await con.OpenAsync();

                        using (SqlDataReader r = await cmd.ExecuteReaderAsync())
                        {
                            while (await r.ReadAsync())
                            {
                                historyData.Add(new
                                {
                                    id = Convert.ToInt32(r["Id"]),
                                    question = r["Question"].ToString()
                                });
                            }
                        }
                    }
                }
                return Json(new { success = true, data = historyData }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        [HttpGet]
        public JsonResult GetUserProfile()
        {
            string userIdStr = Session["UserId"]?.ToString();
            string username = Session["Username"]?.ToString();

            if (string.IsNullOrEmpty(userIdStr) || string.IsNullOrEmpty(username))
                return Json(new { success = false, error = "Session expired" }, JsonRequestBehavior.AllowGet);

            return Json(new
            {
                success = true,
                userId = userIdStr,
                username = username
            }, JsonRequestBehavior.AllowGet);
        }
        [HttpGet]
        public async Task<JsonResult> GetChatMessages(int chatId)
        {
            string userIdStr = Session["UserId"]?.ToString();

            if (string.IsNullOrEmpty(userIdStr))
                return Json(new { success = false, error = "Session expired" }, JsonRequestBehavior.AllowGet);

            int userId = Convert.ToInt32(userIdStr);

            try
            {
                using (SqlConnection con = new SqlConnection(cs))
                {
                    string query = "SELECT Question, Answer FROM ChatHistory WHERE Id = @chatId AND UserId = @u";
                    using (SqlCommand cmd = new SqlCommand(query, con))
                    {
                        cmd.Parameters.AddWithValue("@chatId", chatId);
                        cmd.Parameters.AddWithValue("@u", userId);
                        await con.OpenAsync();

                        using (SqlDataReader r = await cmd.ExecuteReaderAsync())
                        {
                            if (await r.ReadAsync())
                            {
                                return Json(new
                                {
                                    success = true,
                                    question = r["Question"].ToString(),
                                    answer = r["Answer"].ToString()
                                }, JsonRequestBehavior.AllowGet);
                            }
                        }
                    }
                }
                return Json(new { success = false, error = "Chat not found" }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<JsonResult> DeleteChat(int chatId)
        {
            string userIdStr = Session["UserId"]?.ToString();

            if (string.IsNullOrEmpty(userIdStr))
                return Json(new { success = false, error = "Session expired" });

            int userId = Convert.ToInt32(userIdStr);

            try
            {
                using (SqlConnection con = new SqlConnection(cs))
                {
                    string query = "DELETE FROM ChatHistory WHERE Id = @chatId AND UserId = @u";
                    using (SqlCommand cmd = new SqlCommand(query, con))
                    {
                        cmd.Parameters.AddWithValue("@chatId", chatId);
                        cmd.Parameters.AddWithValue("@u", userId);
                        await con.OpenAsync();
                        await cmd.ExecuteNonQueryAsync();
                    }
                }
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        [HttpPost]
        [ValidateInput(false)]
        public async Task<JsonResult> Ask(string question)
        {
            string userIdStr = Session["UserId"]?.ToString();

            if (string.IsNullOrEmpty(userIdStr))
                return Json(new { error = "Session expired. Please login again." });

            int userId = Convert.ToInt32(userIdStr);

            if (string.IsNullOrWhiteSpace(question))
                return Json(new { error = "Question cannot be empty." });

            try
            {
                string answer = GetStaticResponse(question.Trim().ToLower());

                using (SqlConnection con = new SqlConnection(cs))
                {
                    await con.OpenAsync();

                    int quota = 0, used = 0;
                    string quotaQuery = "SELECT Quota, UsedCount FROM Users WHERE Id=@id";
                    using (SqlCommand qCmd = new SqlCommand(quotaQuery, con))
                    {
                        qCmd.Parameters.AddWithValue("@id", userId);
                        using (var r = await qCmd.ExecuteReaderAsync())
                        {
                            if (await r.ReadAsync())
                            {
                                quota = Convert.ToInt32(r["Quota"]);
                                used = Convert.ToInt32(r["UsedCount"]);
                            }
                        }
                    }

                    if (used >= quota)
                        return Json(new { error = "Your limit has been reached.", quotaExhausted = true });

                    string updateQuery = @"
                        INSERT INTO ChatHistory(UserId, Question, Answer) VALUES(@u, @q, @a);
                        UPDATE Users SET UsedCount = UsedCount + 1 WHERE Id=@id;";

                    using (SqlCommand cmd = new SqlCommand(updateQuery, con))
                    {
                        cmd.Parameters.AddWithValue("@u", userId);
                        cmd.Parameters.AddWithValue("@q", question.Trim());
                        cmd.Parameters.AddWithValue("@a", answer);
                        cmd.Parameters.AddWithValue("@id", userId);
                        await cmd.ExecuteNonQueryAsync();
                    }

                    int newUsed = used + 1;
                    int remaining = quota - newUsed;
                    double percentage = quota > 0 ? ((double)remaining / quota) * 100 : 0;

                    return Json(new
                    {
                        success = true,
                        answer = answer,
                        remaining = remaining,
                        percentage = Math.Round(percentage, 1),
                        quota = quota,
                        used = newUsed
                    });
                }
            }
            catch (Exception ex)
            {
                return Json(new { error = "Database Error: " + ex.Message });
            }
        }

        private string GetStaticResponse(string query)
        {
            var qaPairs = new Dictionary<string, string>
            {
                { "capital of france", "The capital of France is Paris." },
                { "who discovered gravity", "Sir Isaac Newton discovered gravity." },
                { "largest planet", "Jupiter is the largest planet in our solar system." },
                { "speed of light", "The speed of light is approximately 299,792 kilometers per second." },
                { "who wrote hamlet", "William Shakespeare wrote Hamlet." },
                { "boiling point of water", "The boiling point of water is 100°C at sea level." },
                { "smallest planet", "Mercury is the smallest planet in our solar system." },
                { "hottest planet", "Venus is the hottest planet in our solar system." },
                { "who painted mona lisa", "Leonardo da Vinci painted the Mona Lisa." },
                { "longest river", "The Nile is the longest river in the world." },
                { "largest ocean", "The Pacific Ocean is the largest ocean on Earth." },
                { "tallest mountain", "Mount Everest is the tallest mountain in the world." },
                { "who invented telephone", "Alexander Graham Bell invented the telephone." },
                { "who invented light bulb", "Thomas Edison invented the light bulb." },
                { "capital of pakistan", "The capital of Pakistan is Islamabad." },
                { "capital of india", "The capital of India is New Delhi." },
                { "largest country", "Russia is the largest country in the world by area." },
                { "smallest country", "Vatican City is the smallest country in the world." },
                { "how many continents", "There are 7 continents on Earth." },
                { "how many oceans", "There are 5 oceans on Earth." }
            };

            foreach (var pair in qaPairs)
            {
                if (query.Contains(pair.Key) || pair.Key.Contains(query))
                    return pair.Value;
            }

            return "I'm sorry, I don't have an answer for that question right now. Please try asking something else!";
        }
    }
}