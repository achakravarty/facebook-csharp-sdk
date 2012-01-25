﻿// --------------------------------
// <copyright file="FacebookClient.cs" company="Thuzi LLC (www.thuzi.com)">
//     Microsoft Public License (Ms-PL)
// </copyright>
// <author>Nathan Totten (ntotten.com), Jim Zimmerman (jimzimmerman.com) and Prabir Shrestha (prabir.me)</author>
// <license>Released under the terms of the Microsoft Public License (Ms-PL)</license>
// <website>https://github.com/facebook-csharp-sdk/facbook-csharp-sdk</website>
// ---------------------------------

namespace Facebook
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.IO;
    using System.Globalization;
    using System.Linq;
    using System.Net;
#if TYPEINFO
    using System.Reflection;
#endif
    using System.Text;
    using FluentHttp;

    /// <summary>
    /// Provides access to the Facbook Platform.
    /// </summary>
    public partial class FacebookClient
    {
        private const int BufferSize = 1024 * 4; // 4kb
        private const string AttachmentMustHavePropertiesSetError = "Attachment (FacebookMediaObject/FacebookMediaStream) must have a content type, file name, and value set.";
        private const string AttachmentValueIsNull = "The value of attachment (FacebookMediaObject/FacebookMediaStream) is null.";
        private const string UnknownResponse = "Unknown facebook response.";
        private const string MultiPartFormPrefix = "--";
        private const string MultiPartNewLine = "\r\n";
        private const string ETagKey = "_etag_";

        private static readonly string[] LegacyRestApiReadOnlyCalls = new[] {
            "admin.getallocation",
            "admin.getappproperties",
            "admin.getbannedusers",
            "admin.getlivestreamvialink",
            "admin.getmetrics",
            "admin.getrestrictioninfo",
            "application.getpublicinfo",
            "auth.getapppublickey",
            "auth.getsession",
            "auth.getsignedpublicsessiondata",
            "comments.get",
            "connect.getunconnectedfriendscount",
            "dashboard.getactivity",
            "dashboard.getcount",
            "dashboard.getglobalnews",
            "dashboard.getnews",
            "dashboard.multigetcount",
            "dashboard.multigetnews",
            "data.getcookies",
            "events.get",
            "events.getmembers",
            "fbml.getcustomtags",
            "feed.getappfriendstories",
            "feed.getregisteredtemplatebundlebyid",
            "feed.getregisteredtemplatebundles",
            "fql.multiquery",
            "fql.query",
            "friends.arefriends",
            "friends.get",
            "friends.getappusers",
            "friends.getlists",
            "friends.getmutualfriends",
            "gifts.get",
            "groups.get",
            "groups.getmembers",
            "intl.gettranslations",
            "links.get",
            "notes.get",
            "notifications.get",
            "pages.getinfo",
            "pages.isadmin",
            "pages.isappadded",
            "pages.isfan",
            "permissions.checkavailableapiaccess",
            "permissions.checkgrantedapiaccess",
            "photos.get",
            "photos.getalbums",
            "photos.gettags",
            "profile.getinfo",
            "profile.getinfooptions",
            "stream.get",
            "stream.getcomments",
            "stream.getfilters",
            "users.getinfo",
            "users.getloggedinuser",
            "users.getstandardinfo",
            "users.hasapppermission",
            "users.isappuser",
            "users.isverified",
            "video.getuploadlimits" 
        };

        private string _accessToken;
        private bool _isSecureConnection;
        private bool _useFacebookBeta;

        private Func<object, string> _serializeJson;
        private static Func<object, string> _defaultJsonSerializer;

        private Func<string, Type, object> _deserializeJson;
        private static Func<string, Type, object> _defaultJsonDeserializer;

        private Func<Uri, HttpWebRequestWrapper> _httpWebRequestFactory;
        private static Func<Uri, HttpWebRequestWrapper> _defaultHttpWebRequestFactory;

        /// <remarks>For unit testing</remarks>
        internal Func<string> Boundary { get; set; }

        /// <summary>
        /// Gets or sets the access token.
        /// </summary>
        public virtual string AccessToken
        {
            get { return _accessToken; }
            set { _accessToken = value; }
        }

        /// <summary>
        /// Gets or sets the value indicating whether to add return_ssl_resource as default parameter in every request.
        /// </summary>
        public virtual bool IsSecureConnection
        {
            get { return _isSecureConnection; }
            set { _isSecureConnection = value; }
        }

        /// <summary>
        /// Gets or sets the value indicating whether to use Facebook beta.
        /// </summary>
        public virtual bool UseFacebookBeta
        {
            get { return _useFacebookBeta; }
            set { _useFacebookBeta = value; }
        }

        /// <summary>
        /// Serialize object to json.
        /// </summary>
        public virtual Func<object, string> SerializeJson
        {
            get { return _serializeJson ?? (_serializeJson = _defaultJsonSerializer); }
            set { _serializeJson = value; }
        }

        /// <summary>
        /// Deserialize json to object.
        /// </summary>
        public virtual Func<string, Type, object> DeserializeJson
        {
            get { return _deserializeJson; }
            set { _deserializeJson = value ?? (_deserializeJson = _defaultJsonDeserializer); ; }
        }

        /// <summary>
        /// Http web request factory.
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public virtual Func<Uri, HttpWebRequestWrapper> HttpWebRequestFactory
        {
            get { return _httpWebRequestFactory; }
            set { _httpWebRequestFactory = value ?? (_httpWebRequestFactory = _defaultHttpWebRequestFactory); }
        }

        static FacebookClient()
        {
            SetDefaultJsonSerializers(null, null);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="FacebookClient"/> class.
        /// </summary>
        public FacebookClient()
        {
            _deserializeJson = _defaultJsonDeserializer;
            _httpWebRequestFactory = _defaultHttpWebRequestFactory;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="FacebookClient"/> class.
        /// </summary>
        /// <param name="accessToken">The facebook access_token.</param>
        /// <exception cref="ArgumentNullException">Access token in null or empty.</exception>
        public FacebookClient(string accessToken)
            : this()
        {
            if (string.IsNullOrEmpty(accessToken))
                throw new ArgumentNullException("accessToken");

            _accessToken = accessToken;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="FacebookClient"/> class.
        /// </summary>
        /// <param name="appId"></param>
        /// <param name="appSecret"></param>
        /// <exception cref="ArgumentNullException"></exception>
        public FacebookClient(string appId, string appSecret)
            : this()
        {
            if (string.IsNullOrEmpty(appId))
                throw new ArgumentNullException("appId");
            if (string.IsNullOrEmpty(appSecret))
                throw new ArgumentNullException("appSecret");

            _accessToken = string.Concat(appId, "|", appSecret);
        }

        /// <summary>
        /// Sets the default json seriazliers and deserializers.
        /// </summary>
        /// <param name="jsonSerializer">Json serializer</param>
        /// <param name="jsonDeserializer">Jsonn deserializer</param>
        public static void SetDefaultJsonSerializers(Func<object, string> jsonSerializer, Func<string, Type, object> jsonDeserializer)
        {
            _defaultJsonSerializer = jsonSerializer ?? SimpleJson.SimpleJson.SerializeObject;
            _defaultJsonDeserializer = jsonDeserializer ?? SimpleJson.SimpleJson.DeserializeObject;
        }

        /// <summary>
        /// Sets the default http web request factory.
        /// </summary>
        /// <param name="httpWebRequestFactory"></param>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static void SetDefaultHttpWebRequestFactory(Func<Uri, HttpWebRequestWrapper> httpWebRequestFactory)
        {
            _defaultHttpWebRequestFactory = httpWebRequestFactory;
        }

        private HttpHelper PrepareRequest(string httpMethod, string path, object parameters, Type resultType, out Stream input, out bool containsEtag)
        {
            if (string.IsNullOrEmpty(httpMethod))
                throw new ArgumentNullException("httpMethod");
            if (string.IsNullOrEmpty(path))
                throw new ArgumentNullException("path");

            input = null;
            containsEtag = false;
            httpMethod = httpMethod.ToUpperInvariant();

            IDictionary<string, FacebookMediaObject> mediaObjects;
            IDictionary<string, FacebookMediaStream> mediaStreams;
            IDictionary<string, object> parametersWithoutMediaObjects = ToDictionary(parameters, out mediaObjects, out mediaStreams) ?? new Dictionary<string, object>();

            if (!parametersWithoutMediaObjects.ContainsKey("access_token") && !string.IsNullOrEmpty(AccessToken))
                parametersWithoutMediaObjects["access_token"] = AccessToken;
            if (!parametersWithoutMediaObjects.ContainsKey("return_ssl_resources") && IsSecureConnection)
                parametersWithoutMediaObjects["return_ssl_resources"] = true;

            string etag = null;
            if (parametersWithoutMediaObjects.ContainsKey(ETagKey))
            {
                etag = (string)parametersWithoutMediaObjects[ETagKey];
                parametersWithoutMediaObjects.Remove(ETagKey);
                containsEtag = true;
            }

            Uri uri;
            bool isLegacyRestApi = false;
            path = ParseUrlQueryString(path, parametersWithoutMediaObjects, false, out uri, out isLegacyRestApi);

            if (parametersWithoutMediaObjects.ContainsKey("format"))
                parametersWithoutMediaObjects["format"] = "json-strings";

            string restMethod = null;
            if (parametersWithoutMediaObjects.ContainsKey("method"))
            {
                restMethod = (string)parametersWithoutMediaObjects["method"];
                if (restMethod.Equals("DELETE", StringComparison.OrdinalIgnoreCase))
                    throw new ArgumentException("Parameter cannot contain method=delete. Use Delete or DeleteAsync or DeleteTaskAsync methods instead.", "parameters");
                isLegacyRestApi = true;
            }
            else if (isLegacyRestApi)
            {
                throw new ArgumentException("Parameters should contain rest 'method' name", "parameters");
            }

            UriBuilder uriBuilder;
            if (uri == null)
            {
                uriBuilder = new UriBuilder { Scheme = "https" };

                if (isLegacyRestApi)
                {
                    if (string.IsNullOrEmpty(restMethod))
                        throw new InvalidOperationException("Legacy rest api 'method' in parameters is null or empty.");
                    if (restMethod.Equals("video.upload"))
                        uriBuilder.Host = UseFacebookBeta ? "api-video.beta.facebook.com" : "api-video.facebook.com";
                    else if (LegacyRestApiReadOnlyCalls.Contains(restMethod))
                        uriBuilder.Host = UseFacebookBeta ? "api-read.beta.facebook.com" : "api-read.facebook.com";
                    else
                        uriBuilder.Host = UseFacebookBeta ? "api.beta.facebook.com" : "api.facebook.com";
                }
                else
                {
                    if (httpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase) && path.EndsWith("/videos"))
                        uriBuilder.Host = UseFacebookBeta ? "graph-video.beta.facebook.com" : "graph-video.facebook.com";
                    else
                        uriBuilder.Host = UseFacebookBeta ? "graph.beta.facebook.com" : "graph.facebook.com";
                }
            }
            else
            {
                uriBuilder = new UriBuilder { Host = uri.Host, Scheme = uri.Scheme };
            }

            uriBuilder.Path = path;

            string contentType = null;
            var queryString = new StringBuilder();

            IList<string> keysThatAreNotString = new List<string>();
            foreach (var key in parametersWithoutMediaObjects.Keys)
            {
                if (!(parametersWithoutMediaObjects[key] is string))
                    keysThatAreNotString.Add(key);
            }

            foreach (var key in keysThatAreNotString)
                parametersWithoutMediaObjects[key] = SerializeJson(parametersWithoutMediaObjects[key]);

            if (parametersWithoutMediaObjects.ContainsKey("access_token"))
            {
                var accessToken = parametersWithoutMediaObjects["access_token"];
                if (accessToken != null && (!(accessToken is string) || (!string.IsNullOrEmpty((string)accessToken))))
                    queryString.AppendFormat("access_token={0}&", accessToken);

                parametersWithoutMediaObjects.Remove("access_token");
            }

            if (!httpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase))
            {
                // for GET,DELETE
                if (mediaObjects.Count > 0 && mediaStreams.Count > 0)
                    throw new InvalidOperationException("Attachments (FacebookMediaObject/FacebookMediaStream) are valid only in POST requests.");

#if SILVERLIGHT
                if (httpMethod.Equals("DELETE", StringComparison.OrdinalIgnoreCase))
                    queryString.Append("method=delete&");
#endif
                foreach (var kvp in parametersWithoutMediaObjects)
                    queryString.AppendFormat("{0}={1}&", HttpHelper.UrlEncode(kvp.Key), HttpHelper.UrlEncode(BuildHttpQuery(kvp.Value, HttpHelper.UrlEncode)));
            }
            else
            {
                if (mediaObjects.Count == 0 && mediaStreams.Count == 0)
                {
                    contentType = "application/x-www-form-urlencoded";
                    var sb = new StringBuilder();
                    foreach (var kvp in parametersWithoutMediaObjects)
                        sb.AppendFormat("{0}={1}&", HttpHelper.UrlEncode(kvp.Key), HttpHelper.UrlEncode(BuildHttpQuery(kvp.Value, HttpHelper.UrlEncode)));
                    if (sb.Length > 0)
                        sb.Length--;
                    input = sb.Length == 0 ? null : new MemoryStream(Encoding.UTF8.GetBytes(sb.ToString()));
                }
                else
                {
                    string boundary = Boundary == null
                                          ? DateTime.UtcNow.Ticks.ToString("x", CultureInfo.InvariantCulture) // for unit testing
                                          : Boundary();

                    contentType = string.Concat("multipart/form-data; boundary=", boundary);

                    var streams = new List<Stream>();
                    var indexOfDisposableStreams = new List<int>();

                    // Build up the post message header
                    var sb = new StringBuilder();

                    foreach (var kvp in parametersWithoutMediaObjects)
                    {
                        sb.Append(MultiPartFormPrefix).Append(boundary).Append(MultiPartNewLine);
                        sb.Append("Content-Disposition: form-data; name=\"").Append(kvp.Key).Append("\"");
                        sb.Append(MultiPartNewLine).Append(MultiPartNewLine);
                        sb.Append(BuildHttpQuery(kvp.Value, HttpHelper.UrlEncode));
                        sb.Append(MultiPartNewLine);
                    }

                    indexOfDisposableStreams.Add(streams.Count);
                    streams.Add(new MemoryStream(Encoding.UTF8.GetBytes(sb.ToString())));

                    foreach (var facebookMediaObject in mediaObjects)
                    {
                        var sbMediaObject = new StringBuilder();
                        var mediaObject = facebookMediaObject.Value;

                        if (mediaObject.ContentType == null || mediaObject.GetValue() == null || string.IsNullOrEmpty(mediaObject.FileName))
                            throw new InvalidOperationException(AttachmentMustHavePropertiesSetError);

                        sbMediaObject.Append(MultiPartFormPrefix).Append(boundary).Append(MultiPartNewLine);
                        sbMediaObject.Append("Content-Disposition: form-data; name=\"").Append(facebookMediaObject.Key).Append("\"; filename=\"").Append(mediaObject.FileName).Append("\"").Append(MultiPartNewLine);
                        sbMediaObject.Append("Content-Type: ").Append(mediaObject.ContentType).Append(MultiPartNewLine).Append(MultiPartNewLine);

                        indexOfDisposableStreams.Add(streams.Count);
                        streams.Add(new MemoryStream(Encoding.UTF8.GetBytes(sbMediaObject.ToString())));

                        byte[] fileData = mediaObject.GetValue();

                        if (fileData == null)
                            throw new InvalidOperationException(AttachmentValueIsNull);

                        indexOfDisposableStreams.Add(streams.Count);
                        streams.Add(new MemoryStream(fileData));
                        indexOfDisposableStreams.Add(streams.Count);
                        streams.Add(new MemoryStream(Encoding.UTF8.GetBytes(MultiPartNewLine)));
                    }

                    foreach (var facebookMediaStream in mediaStreams)
                    {
                        var sbMediaStream = new StringBuilder();
                        var mediaStream = facebookMediaStream.Value;

                        if (mediaStream.ContentType == null || mediaStream.GetValue() == null || string.IsNullOrEmpty(mediaStream.FileName))
                            throw new InvalidOperationException(AttachmentMustHavePropertiesSetError);

                        sbMediaStream.Append(MultiPartFormPrefix).Append(boundary).Append(MultiPartNewLine);
                        sbMediaStream.Append("Content-Disposition: form-data; name=\"").Append(facebookMediaStream.Key).Append("\"; filename=\"").Append(mediaStream.FileName).Append("\"").Append(MultiPartNewLine);
                        sbMediaStream.Append("Content-Type: ").Append(mediaStream.ContentType).Append(MultiPartNewLine).Append(MultiPartNewLine);

                        indexOfDisposableStreams.Add(streams.Count);
                        streams.Add(new MemoryStream(Encoding.UTF8.GetBytes(sbMediaStream.ToString())));

                        var stream = mediaStream.GetValue();

                        if (stream == null)
                            throw new InvalidOperationException(AttachmentValueIsNull);

                        streams.Add(stream);

                        indexOfDisposableStreams.Add(streams.Count);
                        streams.Add(new MemoryStream(Encoding.UTF8.GetBytes(MultiPartNewLine)));
                    }

                    indexOfDisposableStreams.Add(streams.Count);
                    streams.Add(new MemoryStream(Encoding.UTF8.GetBytes(string.Concat(MultiPartNewLine, MultiPartFormPrefix, boundary, MultiPartFormPrefix, MultiPartNewLine))));
                    input = new CombinationStream(streams, indexOfDisposableStreams);
                }
            }

            if (queryString.Length > 0)
                queryString.Length--;

            uriBuilder.Query = queryString.ToString();

            var request = HttpWebRequestFactory == null
                             ? new HttpWebRequestWrapper((HttpWebRequest)WebRequest.Create(uriBuilder.Uri))
                             : HttpWebRequestFactory(uriBuilder.Uri);
            request.Method = httpMethod;
            request.ContentType = contentType;

            if (!string.IsNullOrEmpty(etag))
                request.Headers[HttpRequestHeader.IfNoneMatch] = string.Concat('"', etag, '"');

#if !(SILVERLIGHT || WINRT)
            request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
#endif

            if (input != null)
                request.TrySetContentLength(input.Length);

            return new HttpHelper(request);
        }

        private object ProcessResponse(HttpHelper httpHelper, string responseString, Type resultType, bool containsEtag)
        {
            try
            {
                var response = httpHelper.HttpWebResponse;
                object result = null;

                if (response == null)
                    throw new InvalidOperationException(UnknownResponse);

                if (response.ContentType.Contains("text/javascript"))
                {
                    result = DeserializeJson(responseString, resultType);
                }
                else if (response.StatusCode == HttpStatusCode.OK && response.ContentType.Contains("text/plain"))
                {
                    if (response.ResponseUri.AbsolutePath == "/oauth/access_token")
                    {
                        var body = new JsonObject();
                        foreach (var kvp in responseString.Split('&'))
                        {
                            var split = kvp.Split('=');
                            if (split.Length == 2)
                                body[split[0]] = split[1];
                        }

                        if (body.ContainsKey("expires"))
                            body["expires"] = Convert.ToInt64(body["expires"]);

                        result = resultType == null ? body : DeserializeJson(body.ToString(), resultType);

                        return result;
                    }
                    else
                    {
                        throw new InvalidOperationException(UnknownResponse);
                    }
                }
                else
                {
                    throw new InvalidOperationException(UnknownResponse);
                }

                var exception = GetException(httpHelper, result);
                if (exception == null)
                {
                    if (containsEtag)
                    {
                        var json = new JsonObject();

                        var headers = new JsonObject();
                        foreach (var headerName in response.Headers.AllKeys)
                            headers[headerName] = response.Headers[headerName];

                        json["headers"] = headers;
                        json["body"] = result;

                        return json;
                    }

                    return result;
                }

                throw exception;

            }
            catch (Exception ex)
            {
                if (ex is FacebookApiException)
                    throw;

                if (httpHelper.InnerException != null)
                    throw httpHelper.InnerException;

                throw;
            }
        }

        internal static Exception GetException(HttpHelper httpHelper, object result)
        {
            var response = httpHelper.HttpWebResponse;
            var responseUri = response.ResponseUri;

            if (result == null)
                return null;

            var responseDict = result as IDictionary<string, object>;
            if (responseDict == null)
                return null;

            FacebookApiException resultException = null;

            if (responseUri.Host == "api.facebook.com" ||
                responseUri.Host == "api-read.facebook.com" ||
                responseUri.Host == "api-video.facebook.com" ||
                responseUri.Host == "api.beta.facebook.com" ||
                responseUri.Host == "api-read.beta.facebook.com" ||
                responseUri.Host == "api-video.facebook.com")
            {
                if (responseDict.ContainsKey("error_code"))
                {
                    string errorCode = responseDict["error_code"].ToString();
                    string errorMsg = null;
                    if (responseDict.ContainsKey("error_msg"))
                        errorMsg = responseDict["error_msg"] as string;

                    // Error Details: http://wiki.developers.facebook.com/index.php/Error_codes
                    if (errorCode == "190")
                        resultException = new FacebookOAuthException(errorMsg, errorCode);
                    else if (errorCode == "4" || errorCode == "API_EC_TOO_MANY_CALLS" || (errorMsg != null && errorMsg.Contains("request limit reached")))
                        resultException = new FacebookApiLimitException(errorMsg, errorCode);
                    else
                        resultException = new FacebookApiException(errorMsg, errorCode);
                }
            }
            else
            {
                if (responseDict.ContainsKey("error"))
                {
                    var error = responseDict["error"] as IDictionary<string, object>;
                    if (error != null)
                    {
                        var errorType = error["type"] as string;
                        var errorMessage = error["message"] as string;

                        // Check to make sure the correct data is in the response
                        if (!string.IsNullOrEmpty(errorType) && !string.IsNullOrEmpty(errorMessage))
                        {
                            // We don't include the inner exception because it is not needed and is always a WebException.
                            // It is easier to understand the error if we use Facebook's error message.
                            if (errorType == "OAuthException")
                                resultException = new FacebookOAuthException(errorMessage, errorType);
                            else if (errorType == "API_EC_TOO_MANY_CALLS" || (errorMessage.Contains("request limit reached")))
                                resultException = new FacebookApiLimitException(errorMessage, errorType);
                            else
                                resultException = new FacebookApiException(errorMessage, errorType);
                        }
                    }
                    else
                    {
                        long? errorNumber = null;
                        if (responseDict["error"] is long)
                            errorNumber = (long)responseDict["error"];
                        if (errorNumber == null && responseDict["error"] is int)
                            errorNumber = (int)responseDict["error"];
                        string errorDescription = null;
                        if (responseDict.ContainsKey("error_description"))
                            errorDescription = responseDict["error_description"] as string;
                        if (errorNumber != null && !string.IsNullOrEmpty(errorDescription))
                        {
                            if (errorNumber == 190)
                                resultException = new FacebookOAuthException(errorDescription, "API_EC_PARAM_ACCESS_TOKEN");
                            else
                                resultException = new FacebookApiException(errorDescription, errorNumber.Value.ToString(CultureInfo.InvariantCulture));
                        }
                    }
                }
            }

            return resultException;
        }

        /// <summary>
        /// Converts the parameters to IDictionary&lt;string,object&gt;
        /// </summary>
        /// <param name="parameters">The parameter to convert.</param>
        /// <param name="mediaObjects">The extracted Facebook media objects.</param>
        /// <param name="mediaStreams">The extracted Facebook media streams.</param>
        /// <returns>The converted dictionary.</returns>
        private static IDictionary<string, object> ToDictionary(object parameters, out IDictionary<string, FacebookMediaObject> mediaObjects, out IDictionary<string, FacebookMediaStream> mediaStreams)
        {
            mediaObjects = new Dictionary<string, FacebookMediaObject>();
            mediaStreams = new Dictionary<string, FacebookMediaStream>();

            var dictionary = parameters as IDictionary<string, object>;

            // todo: IEnumerable<KeyValuePair<T1,T2>> and IEnumerable<Tuple<T1, T2>>

            if (dictionary == null)
            {
                if (parameters == null)
                    return null;

                // convert anonymous objects / unknown objects to IDictionary<string, object>
                dictionary = new Dictionary<string, object>();
#if TYPEINFO
                foreach (var propertyInfo in parameters.GetType().GetTypeInfo().DeclaredProperties.Where(p => p.CanRead))
                {
                    dictionary[propertyInfo.Name] = propertyInfo.GetValue(parameters, null);
                }
#else
                foreach (var propertyInfo in parameters.GetType().GetProperties())
                {
                    if (!propertyInfo.CanRead) continue;
                    dictionary[propertyInfo.Name] = propertyInfo.GetValue(parameters, null);
                }
#endif
            }

            foreach (var parameter in dictionary)
            {
                if (parameter.Value is FacebookMediaObject)
                    mediaObjects.Add(parameter.Key, (FacebookMediaObject)parameter.Value);
                else if (parameter.Value is FacebookMediaStream)
                    mediaStreams.Add(parameter.Key, (FacebookMediaStream)parameter.Value);
            }

            foreach (var mediaObject in mediaObjects)
                dictionary.Remove(mediaObject.Key);

            foreach (var mediaStream in mediaStreams)
                dictionary.Remove(mediaStream.Key);

            return dictionary;
        }

        /// <summary>
        /// Converts the parameters to http query.
        /// </summary>
        /// <param name="parameter">The parameter to convert.</param>
        /// <param name="encode">Url encoder function.</param>
        /// <returns>The http query.</returns>
        /// <remarks>
        /// The result is not url encoded. The caller needs to take care of url encoding the result.
        /// </remarks>
        private static string BuildHttpQuery(object parameter, Func<string, string> encode)
        {
            if (parameter == null)
                return "null";
            if (parameter is string)
                return (string)parameter;
            if (parameter is bool)
                return (bool)parameter ? "true" : "false";

            if (parameter is int || parameter is long ||
                parameter is float || parameter is double || parameter is decimal ||
                parameter is byte || parameter is sbyte ||
                parameter is short || parameter is ushort ||
                parameter is uint || parameter is ulong)
                return parameter.ToString();

            // todo: IEnumerable<KeyValuePair<T1,T2>> and IEnumerable<Tuple<T1, T2>>

            var sb = new StringBuilder();
            if (parameter is IEnumerable<KeyValuePair<string, object>>)
            {
                foreach (var kvp in (IEnumerable<KeyValuePair<string, object>>)parameter)
                    sb.AppendFormat("{0}={1}&", encode(kvp.Key), encode(BuildHttpQuery(kvp.Value, encode)));
            }
            else if (parameter is IEnumerable<KeyValuePair<string, string>>)
            {
                foreach (var kvp in (IEnumerable<KeyValuePair<string, string>>)parameter)
                    sb.AppendFormat("{0}={1}&", encode(kvp.Key), encode(kvp.Value));
            }
            else if (parameter is IEnumerable)
            {
                foreach (var obj in (IEnumerable)parameter)
                    sb.AppendFormat("{0},", encode(BuildHttpQuery(obj, encode)));
            }
            else if (parameter is DateTime)
            {
                return DateTimeConvertor.ToIso8601FormattedDateTime((DateTime)parameter);
            }
            else
            {
                IDictionary<string, FacebookMediaObject> mediaObjects;
                IDictionary<string, FacebookMediaStream> mediaStreams;
                var dict = ToDictionary(parameter, out mediaObjects, out mediaStreams);
                if (mediaObjects.Count > 0 || mediaStreams.Count > 0)
                    throw new InvalidOperationException("Parameter can contain attachements (FacebookMediaObject/FacebookMediaStream) only in the top most level.");
                return BuildHttpQuery(dict, encode);
            }

            if (sb.Length > 0)
                sb.Length--;

            return sb.ToString();
        }

        private static string ParseUrlQueryString(string path, IDictionary<string, object> parameters, bool forceParseAllUrls, out Uri uri, out bool isLegacyRestApi)
        {
            if (parameters == null)
                throw new ArgumentNullException("parameters");

            isLegacyRestApi = false;
            uri = null;
            if (Uri.TryCreate(path, UriKind.Absolute, out uri))
            {
                if (forceParseAllUrls)
                {
                    path = string.Concat(uri.AbsolutePath, uri.Query);
                }
                else
                {
                    switch (uri.Host)
                    {
                        // graph api
                        case "graph.facebook.com":
                        case "graph-video.facebook.com":
                        case "graph.beta.facebook.com":
                        case "graph-video.beta.facebook.com":
                            // If the host is graph.facebook.com the user has passed in the full url.
                            // We remove the host part and continue with the parsing.
                            path = string.Concat(uri.AbsolutePath, uri.Query);
                            break;
                        // legacy rest api
                        case "api.facebook.com":
                        case "api-read.facebook.com":
                        case "api-video.facebook.com":
                        case "api.beta.facebook.com":
                        case "api-read.beta.facebook.com":
                        case "api-video.beta.facebook.com":
                            // If the host is graph.facebook.com the user has passed in the full url.
                            // We remove the host part and continue with the parsing.
                            isLegacyRestApi = true;
                            path = string.Concat(uri.AbsolutePath, uri.Query);
                            break;
                        default:
                            // If the url is a valid absolute url we are passing the full url as the 'id'
                            // parameter of the query. For example, if path is something like
                            // http://www.microsoft.com/page.aspx?id=23 it means that we are trying
                            // to make the request https://graph.facebook.com/http://www.microsoft.com/page.aspx%3Fid%3D23
                            // So we are just going to return the path
                            uri = null;
                            break;
                    }
                }
            }

            if (string.IsNullOrEmpty(path))
                return string.Empty;

            // Clean the path, remove leading '/'.
            if (path.Length > 0 && path[0] == '/')
                path = path.Substring(1);

            // If the url does not have a host it means we are using a url
            // like /me or /me?fields=first_name,last_name so we want to
            // remove the querystring info and add it to parameters
            var parts = path.Split(new[] { '?' });
            path = parts[0]; // Set the path to only the path portion of the url

            if (parts.Length == 2 && parts[1] != null)
            {
                // Add the query string values to the parameters dictionary
                var qs = parts[1];
                var qsItems = qs.Split('&');

                foreach (var kvp in qsItems)
                {
                    if (!string.IsNullOrEmpty(kvp))
                    {
                        var qsPart = kvp.Split('=');
                        if (qsPart.Length == 2 && !string.IsNullOrEmpty(qsPart[0]))
                        {
                            var key = HttpHelper.UrlDecode(qsPart[0]);
                            if (!parameters.ContainsKey(key))
                                parameters[key] = HttpHelper.UrlDecode(qsPart[1]);
                        }
                        else
                            throw new ArgumentException("Invalid path", "path");
                    }
                }
            }
            else if (parts.Length > 2)
            {
                throw new ArgumentException("Invalid path", "path");
            }

            return path;
        }

        internal static string ParseUrlQueryString(string path, IDictionary<string, object> parameters, bool forceParseAllUrls)
        {
            Uri uri;
            bool isLegacyRestApi;
            return ParseUrlQueryString(path, parameters, forceParseAllUrls, out uri, out isLegacyRestApi);
        }
    }
}