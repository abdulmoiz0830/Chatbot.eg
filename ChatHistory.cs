using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace SimpleChatbot.Models
{
    public class ChatHistory
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string Question { get; set; }
        public string Answer { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}