using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

namespace Oxide.Plugins
{
    [Info("Imgur API", "MJSU", "1.0.2")]
    [Description("Allows plugins to upload images to imgur")]
    internal class ImgurApi : CovalencePlugin
    {
        #region Class Fields
        private PluginConfig _pluginConfig; //Plugin Config
        
        private const string ClientIdUrl = "https://api.imgur.com/oauth2/addclient";
        private const string UploadImageUrl = "https://api.imgur.com/3/upload";
        private const string DeleteImageUrl = "https://api.imgur.com/3/image/{0}";
        private const string CreateAlbumUrl = "https://api.imgur.com/3/album";
        private const string DeleteAlbumUrl = "https://api.imgur.com/3/album/{0}";
        private const string AlbumLink = "https://imgur.com/a/{0}";

        private GameObject _go;
        private ImgurBehavior _behavior;
        #endregion

        #region Setup & Loading
        protected override void LoadDefaultConfig()
        {
            PrintWarning("Loading Default Config");
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            Config.Settings.DefaultValueHandling = DefaultValueHandling.Populate;
            _pluginConfig = AdditionalConfig(Config.ReadObject<PluginConfig>());
            Config.WriteObject(_pluginConfig);
        }

        private PluginConfig AdditionalConfig(PluginConfig config)
        {
            return config;
        }

        private void OnServerInitialized()
        {
            _go = new GameObject();
            _behavior = _go.AddComponent<ImgurBehavior>();
        }

        private void Unload()
        {
            _behavior.StopAllCoroutines();
            GameObject.Destroy(_go);
        }
        #endregion

        #region API
        private void UploadImage(byte[] image, Action<Hash<string, object>> callback, string title = null, string description = null)
        {
            if (_pluginConfig.ClientId == ClientIdUrl)
            {
                PrintError($"Please set your Imgur Client Id in the config!");
                return;
            }
            
            _behavior.StartCoroutine(HandleUploadImage(image, response =>
            {
                callback(response.ToHash());
            }, title, description));
        }

        private void DeleteSingleImage(string deleteHash, Action<Hash<string, object>> callback)
        {
            if (_pluginConfig.ClientId == ClientIdUrl)
            {
                PrintError($"Please set your Imgur Client Id in the config!");
                return;
            }
            
            _behavior.StartCoroutine(HandleDeleteSingleImage(deleteHash, callback));
        }

        private void UploadAlbum(List<Hash<string, object>> images, Action<Hash<string, Hash<string, object>>> callback, string title = null, string description = null)
        {
            if (_pluginConfig.ClientId == ClientIdUrl)
            {
                PrintError($"Please set your Imgur Client Id in the config!");
                return;
            }
            
            _behavior.StartCoroutine(HandleUploadAlbum(images, callback, title, description));
        }
        
        private void DeleteAlbum(string deleteHash, Action<Hash<string, object>> callback)
        {
            if (_pluginConfig.ClientId == ClientIdUrl)
            {
                PrintError($"Please set your Imgur Client Id in the config!");
                return;
            }
            
            _behavior.StartCoroutine(HandleDeleteAlbum(deleteHash, callback));
        }
        #endregion

        #region Handlers
        private IEnumerator HandleUploadImage(byte[] image, Action<ApiResponse<UploadResponse>> callback, string title, string description)
        {
            List<IMultipartFormSection> data = new List<IMultipartFormSection>
            {
                new MultipartFormDataSection("image", Convert.ToBase64String(image)),
                new MultipartFormDataSection("type", "base64")
            };
            
            if (!string.IsNullOrEmpty(title))
            {
                data.Add(new MultipartFormDataSection("title", title));
            }

            if (!string.IsNullOrEmpty(description))
            {
                data.Add(new MultipartFormDataSection("description", description));
            }

            yield return HandleImgurRequest(UploadImageUrl, data, callback);
        }
        
        private IEnumerator HandleDeleteSingleImage(string deleteHash, Action<Hash<string, object>> callback)
        {
            yield return HandleImgurRequest<bool>(string.Format(DeleteImageUrl, deleteHash), new List<IMultipartFormSection>(), response =>
            {
                callback(response.ToHash());
            });
        }
        
        private IEnumerator HandleUploadAlbum(List<Hash<string, object>> images, Action<Hash<string, Hash<string, object>>> callback, string title = null, string description = null)
        {
            List<ApiResponse<UploadResponse>> albumImages = new List<ApiResponse<UploadResponse>>();

            foreach (Hash<string, object> uploadData in images)
            {
                byte[] image = uploadData["Image"] as byte[];
                string imageTitle = uploadData["Title"] as string;
                string imageDescription = uploadData["Description"] as string;
                
                yield return HandleUploadImage(image, img =>
                {
                    albumImages.Add(img);
                }, imageTitle, imageDescription);
                
                yield return new WaitForSeconds(1);
            }

            List<IMultipartFormSection> data = albumImages
                .Where(a => a.Success)
                .Select(response => new MultipartFormDataSection("deletehashes[]", response.Data.DeleteHash))
                .Cast<IMultipartFormSection>()
                .ToList();
            
            if (!string.IsNullOrEmpty(title))
            {
                data.Add(new MultipartFormDataSection("title", title));
            }

            if (!string.IsNullOrEmpty(description))
            {
                data.Add(new MultipartFormDataSection("description", description));
            }

            data.Add(new MultipartFormDataSection("type", "base64"));

            yield return HandleImgurRequest<AlbumResponse>(CreateAlbumUrl, data, response =>
            {
                Hash<string, Hash<string, object>> album = new Hash<string, Hash<string, object>>
                {
                    ["Album"] = response.ToHash()
                };

                for (int index = 0; index < albumImages.Count; index++)
                {
                    ApiResponse<UploadResponse> image = albumImages[index];
                    album[$"Image{index}"] = image.ToHash();
                }

                callback(album);
            });
        }
        
        private IEnumerator HandleDeleteAlbum(string deleteHash, Action<Hash<string, object>> callback)
        {
            yield return HandleImgurRequest<bool>(string.Format(DeleteAlbumUrl, deleteHash), new List<IMultipartFormSection>(), response =>
            {
                callback(response.ToHash());
            });
        }
        #endregion

        #region Send Methods
        private IEnumerator HandleImgurRequest<T>(string url, List<IMultipartFormSection> data, Action<ApiResponse<T>> action) 
        {
            UnityWebRequest www = UnityWebRequest.Post(url, data);
            www.SetRequestHeader("Authorization", $"Client-ID {_pluginConfig.ClientId}");
            yield return www.SendWebRequest();

            ApiResponse<T> response;
            if (www.isNetworkError || www.isHttpError)
            {
                response = new ApiResponse<T>
                {
                    Success = false,
                    Status = (int) www.responseCode,
                    Errors = new[] {new ErrorMessage {Detail = www.error}}
                };
            }
            else
            {
                response = JsonConvert.DeserializeObject<ApiResponse<T>>(www.downloadHandler.text);
            }

            action(response);
        }
        #endregion

        #region Behavior
        private class ImgurBehavior : MonoBehaviour
        {
            
        }
        #endregion

        #region Classes
        private class PluginConfig
        {
            [DefaultValue(ClientIdUrl)]
            [JsonProperty(PropertyName = "Imgur Client ID")]
            public string ClientId { get; set; }
        }

        private class ApiResponse<T>
        {
            [JsonProperty(PropertyName = "status")]
            public int Status { get; set; }
            
            [JsonProperty(PropertyName = "success")]
            public bool Success { get; set; }
            
            [JsonProperty(PropertyName = "data")]
            public T Data { get; set; }
            
            [JsonProperty(PropertyName = "errors")]
            public ErrorMessage[] Errors { get; set; }

            public Hash<string, object> ToHash()
            {
                object data;
                BaseDataResponse response = Data as BaseDataResponse;
                if (response != null)
                {
                    data = response.ToHash();
                }
                else
                {
                    data = Data;
                }
                
                return new Hash<string, object>
                {
                    [nameof(Status)] = Status,
                    [nameof(Success)] = Success,
                    [nameof(Data)] = data,
                    [nameof(Errors)] = Errors?.Select(e => e.ToHash()).ToList()
                };
            }
        }

        private class ErrorMessage
        {
            [JsonProperty(PropertyName = "id")]
            public string Id { get; set; }
            
            [JsonProperty(PropertyName = "code")]
            public string Code { get; set; }
            
            [JsonProperty(PropertyName = "status")]
            public string Status { get; set; }
            
            [JsonProperty(PropertyName = "detail")]
            public string Detail { get; set; }

            public Hash<string, object> ToHash()
            {
                return new Hash<string, object>
                {
                    [nameof(Id)] = Id,
                    [nameof(Code)] = Code,
                    [nameof(Status)] = Status,
                    [nameof(Detail)] = Detail
                };
            }
        }

        private abstract class BaseDataResponse
        {
            public abstract Hash<string, object> ToHash();
        }

        private class UploadResponse : BaseDataResponse
        {
            [JsonProperty(PropertyName = "id")]
            public string Id { get; set; }
            
            [JsonProperty(PropertyName = "deletehash")]
            public string DeleteHash { get; set; }
            
            [JsonProperty(PropertyName = "link")]
            public string Link { get; set; }

            public override Hash<string, object> ToHash()
            {
                return new Hash<string, object>
                {
                    [nameof(Id)] = Id,
                    [nameof(DeleteHash)] = DeleteHash,
                    [nameof(Link)] = Link
                };
            }
        }
        
        private class AlbumResponse : BaseDataResponse
        {
            [JsonProperty(PropertyName = "id")]
            public string Id { get; set; }
            
            [JsonProperty(PropertyName = "deletehash")]
            public string DeleteHash { get; set; }

            public string Link => string.Format(AlbumLink, Id);

            public override Hash<string, object> ToHash()
            {
                return new Hash<string, object>
                {
                    [nameof(Id)] = Id,
                    [nameof(DeleteHash)] = DeleteHash,
                    [nameof(Link)] = Link
                };
            }
        }
        #endregion
    }
}
