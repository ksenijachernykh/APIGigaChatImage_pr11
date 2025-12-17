using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APIGigaChatImage.Models.Response
{
    public class ResponseToken
    {
        public string access_token { get; set; }
        public string expires_at { get; set; }
    }

    public class ImageGenerationResponse
    {
        public int created { get; set; }
        public string id { get; set; }
        public List<ImageData> data { get; set; }
    }

    public class ImageData
    {
        public string url { get; set; }
        public string b64_json { get; set; }
    }
}
