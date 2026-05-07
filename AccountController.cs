using System;
using System.Configuration;
using System.Data.SqlClient;
using System.Web;
using System.Web.Mvc;
using System.Web.Security;

namespace SimpleChatbot.Controllers
{
    public class AccountController : Controller
    {
        string cs = ConfigurationManager.ConnectionStrings["DefaultConnection"].ConnectionString;

        [HttpGet]
        [AllowAnonymous]
        public ActionResult Login()
        {
            if (User.Identity.IsAuthenticated)
                return RedirectToAction("Index", "Chat");
            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public ActionResult Login(string username, string password)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                ViewBag.Error = "Username and password are required.";
                return View();
            }

            using (SqlConnection con = new SqlConnection(cs))
            {
                con.Open();
                string q = "SELECT Id, Username FROM Users WHERE Username=@u AND Password=@p";
                SqlCommand cmd = new SqlCommand(q, con);
                cmd.Parameters.AddWithValue("@u", username.Trim());
                cmd.Parameters.AddWithValue("@p", password.Trim());

                using (SqlDataReader r = cmd.ExecuteReader())
                {
                    if (r.Read())
                    {
                        string userId = r["Id"].ToString();
                        string userName = r["Username"].ToString();

                        FormsAuthentication.SetAuthCookie(userName, false);
                        Session["UserId"] = userId;
                        Session["Username"] = userName;

                        return RedirectToAction("Index", "Chat");
                    }
                }
            }

            ViewBag.Error = "Invalid username or password.";
            return View();
        }

        [HttpGet]
        [AllowAnonymous]
        public ActionResult Register()
        {
            if (User.Identity.IsAuthenticated)
                return RedirectToAction("Index", "Chat");
            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public ActionResult Register(string username, string password)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                ViewBag.Error = "Username and password are required.";
                return View();
            }

            if (password.Length < 6)
            {
                ViewBag.Error = "Password must be at least 6 characters.";
                return View();
            }

            using (SqlConnection con = new SqlConnection(cs))
            {
                con.Open();

                string checkQuery = "SELECT COUNT(*) FROM Users WHERE Username=@u";
                using (SqlCommand checkCmd = new SqlCommand(checkQuery, con))
                {
                    checkCmd.Parameters.AddWithValue("@u", username.Trim());
                    int count = (int)checkCmd.ExecuteScalar();
                    if (count > 0)
                    {
                        ViewBag.Error = "Username already exists.";
                        return View();
                    }
                }

                string insertQuery = @"INSERT INTO Users (Username, Password, Quota, UsedCount, CreatedDate) 
                                     VALUES (@u, @p, @quota, @used, @date)";
                using (SqlCommand insertCmd = new SqlCommand(insertQuery, con))
                {
                    insertCmd.Parameters.AddWithValue("@u", username.Trim());
                    insertCmd.Parameters.AddWithValue("@p", password.Trim());
                    insertCmd.Parameters.AddWithValue("@quota", 20);
                    insertCmd.Parameters.AddWithValue("@used", 0);
                    insertCmd.Parameters.AddWithValue("@date", DateTime.Now);
                    insertCmd.ExecuteNonQuery();
                }
            }

            TempData["SuccessMessage"] = "Registration successful! Please login.";
            return RedirectToAction("Login");
        }

        public ActionResult Logout()
        {
            FormsAuthentication.SignOut();
            Session.Clear();
            Session.Abandon();
            return RedirectToAction("Login");
        }
    }
}