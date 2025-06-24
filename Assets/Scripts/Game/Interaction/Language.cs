using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace DLS.Game
{
    public static class Language
    {
        public static string[] Languages { get; private set; }
        public static string[] LanguagesCode { get; private set; }
        public static string currentLanguage = "en-US";
        static JObject keys;
        public static void Refresh()
        {
            JObject languageFile = JObject.Parse(Resources.Load<TextAsset>("Language").text);
            JObject[] languages = ((JArray)languageFile["languages"]).ToObject<JObject[]>();
            keys = (JObject)languageFile["keys"];
            Languages = Array.ConvertAll(languages, e => e["name"].ToString());
            LanguagesCode = Array.ConvertAll(languages, e => e["code"].ToString());
        }
        public static string GetKey(string key, string language) => (string)keys[key][language];
        public static string GetKey(string key) => GetKey(key, currentLanguage);
    }
}