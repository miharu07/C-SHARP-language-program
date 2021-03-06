﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using System.Windows.Input;

namespace 自动进入钉钉直播
{
    class OCR
    {
        // OCR API_KEY
        private static string API_KEY = "ABHVET3G06m8RAmvE7lHCpkn";
        private static string SECRET_KEY = "3vv0bG0P9MkAI0SRgabEgS3Hac8vHQPC";


        // 通过关键字判断钉钉是否在直播
        public static bool Is_Live(string picturePath)
        {
            string apikeyPath, keyWordPath;// 自定义api和关键字文件路径
            string word;                   // 识别出来的文字

            // 文字识别关键字
            char[] key_words = { '小', '初', '高', '大', '班', '中', '学', '群', '正', '在', '直', '播' };
            List<char> customKeyWords = new List<char>();

            // 判断是否有自定义apikey文件和关键字文件
            DirectoryInfo dir = new DirectoryInfo(Application.StartupPath);
            FileInfo[] files = dir.GetFiles("*.txt");// 查找目录下时txt的文件
            foreach (var f in files)
            {
                if (f.ToString().ToLower() == "apikey.txt")
                {
                    apikeyPath = Path.Combine(Application.StartupPath + "\\", f.ToString());
                    ReadApiKeyFile(apikeyPath, out API_KEY, out SECRET_KEY);
                }
                else if (f.ToString().ToLower() == "关键字.txt")
                {
                    keyWordPath = Path.Combine(Application.StartupPath + "\\", f.ToString());
                    using (StreamReader sr = new StreamReader(keyWordPath))
                    {
                        string str = sr.ReadToEnd();
                        customKeyWords = new List<char>(str);
                        while (customKeyWords.Remove('\r') || customKeyWords.Remove('\n'))
                            ; // 去掉读取的换行符
                    }
                }
            }

            // 识别图片文字
            word = GeneralBasic(picturePath);
            if (string.IsNullOrEmpty(word)) // 如果返回结果为空
                return false;

            foreach (char c in word)
            {
                // 如果自定义关键字文件存在且不为空，则从文件查找
                if (customKeyWords.Count > 0)
                {
                    if (customKeyWords.Contains(c))
                        return true;
                }
                // 从关键字数组中查找
                if (Array.IndexOf(key_words, c) != -1)
                    return true;
            }
            return false;
        }


        public static void ReadApiKeyFile(string path, out string ak, out string sk)
        {
            ak = "";
            sk = "";
            using (StreamReader sr = new StreamReader(path))
            {
                string str;
                for (int i = 1; (str = sr.ReadLine()) != null && i <= 2; i++)
                {
                    if (i == 1)
                        ak = str;
                    else if (i == 2)
                        sk = str;
                }
            }
        }


        // 获取AccessToken
        public static void GetAccessToken(out string token, string APIKey = null, string SecretKey = null)
        {
            // 如果传入的密钥为空，则使用默认的密钥
            if (string.IsNullOrEmpty(APIKey) || string.IsNullOrEmpty(SecretKey))
            {
                APIKey = API_KEY;
                SecretKey = SECRET_KEY;
            }

            string url = "https://aip.baidubce.com/oauth/2.0/token";
            List<KeyValuePair<string, string>> paraList = new List<KeyValuePair<string, string>>();
            paraList.Add(new KeyValuePair<string, string>("grant_type", "client_credentials"));
            paraList.Add(new KeyValuePair<string, string>("client_id", APIKey));
            paraList.Add(new KeyValuePair<string, string>("client_secret", SecretKey));

            HttpClient client = new HttpClient();
            HttpResponseMessage response = new HttpResponseMessage();
            string result;
            try
            {
                response = client.PostAsync(url, new FormUrlEncodedContent(paraList)).Result;
                result = response.Content.ReadAsStringAsync().Result;
            }
            catch (Exception ex)
            {
                throw new Exception("(OCR)" + ex.Message);
            }
            finally
            {
                response.Dispose();
                client.Dispose();
            }

            JavaScriptSerializer js = new JavaScriptSerializer();// 实例化一个能够序列化数据的类
            Token list = js.Deserialize<Token>(result);          // 将json数据转化为对象并赋值给list
            token = list.access_token;
            if (list.error != null)
                throw new Exception("(OCR)获取AccessToken失败！" + "\n原因：" + list.error_description);
        }


        // 通用文字识别
        private static string general_basic_host = "https://aip.baidubce.com/rest/2.0/ocr/v1/general_basic?access_token=";
        // 通用文字识别（含位置信息版）
        private static string basic_host = "https://aip.baidubce.com/rest/2.0/ocr/v1/general?access_token=";
        // 通用文字识别（高精度含位置版）
        private static string accurate_host = "https://aip.baidubce.com/rest/2.0/ocr/v1/accurate?access_token=";


        // 调用百度API文字识别
        private static string GeneralBasic(string path)
        {
            string url = null, token, result;

            // 获取文字识别AccessToken
            GetAccessToken(out token);

            for (int i = 1; i < 4; i++)
            {
                if (i == 1)
                    url = general_basic_host + token;
                else if (i == 2)
                    url = basic_host + token;
                else if (i == 3)
                    url = accurate_host + token;

                Encoding encoding = Encoding.Default;
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
                request.Method = "post";
                request.KeepAlive = true;

                string base64 = GetFileBase64(path); // 获取图片的base64编码
                string str = "image=" + HttpUtility.UrlEncode(base64);
                byte[] buffer = encoding.GetBytes(str);
                request.ContentLength = buffer.Length;
                request.GetRequestStream().Write(buffer, 0, buffer.Length);

                HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                StreamReader reader = new StreamReader(response.GetResponseStream(), Encoding.UTF8);
                try
                {
                    result = reader.ReadToEnd();
                }
                catch (Exception ex)
                {
                    throw new Exception("(OCR)" + ex.Message);
                }
                finally
                {
                    response.Dispose();
                    reader.Close();
                    reader.Dispose();
                }

                JavaScriptSerializer js = new JavaScriptSerializer();// 实例化一个能够序列化数据的类
                Json.Root list = js.Deserialize<Json.Root>(result);  // 将json数据转化为对象类型并赋值给list
                if (list.error_code != null) // 如果调用Api出现错误
                {
                    // 如果3个Api都调用了并且都出现了错误
                    if (i == 3)
                    {
                        throw new Exception("OCR识别错误！" + "\n原因：" + list.error_msg);
                    }
                    continue; // 否则继续调用下一个Api
                }

                // 接收序列化后的数据
                StringBuilder builder = new StringBuilder(result.Length);
                foreach (var item in list.words_result)
                    builder.Append(item.words);

                return builder.ToString();
            }
            return null;
        }

        private static string GetFileBase64(string fileName)
        {
            string baser64;
            using (FileStream filestream = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                byte[] arr = new byte[filestream.Length];
                filestream.Read(arr, 0, (int)filestream.Length);
                baser64 = Convert.ToBase64String(arr);
            }
            return baser64;
        }
    }

    class Token
    {
        public string error { get; set; }
        public string error_description { get; set; }
        public string access_token { get; set; }
    }

    class Json
    {
        public class Words_resultItem
        {
            public string words { get; set; }
        }

        public class Root
        {
            public string error_code { get; set; }
            public string error_msg { get; set; }
            public List<Words_resultItem> words_result { get; set; }
        }
    }
}
