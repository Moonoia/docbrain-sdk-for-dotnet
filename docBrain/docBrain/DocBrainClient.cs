using Google.Apis.Auth.OAuth2;
using Google.Cloud.Storage.V1;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using docBrain;

namespace docBrain
{
    public class DocBrainClient
    {
        public DocBrainClient(string inPlatformUrl, 
            string inPlatformAuthorization, 
            string inGoogleAuthenticationToken = null,
            string inBucketCredentials = null, 
            string inUploadBucket = "moonoia-poc-data", 
            string inBucketPath = "data")
        {
            PlatformUrl = inPlatformUrl;
            GoogleAuthenticationToken = inGoogleAuthenticationToken;
            BucketCredentials = inBucketCredentials;
            PlatformAuthorization = inPlatformAuthorization;
            UploadBucket = inUploadBucket;
            BucketPath = inBucketPath;

            _Credentials = GetCredentials();
            _StorageClient = StorageClient.Create(_Credentials);
        }

        private GoogleCredential _Credentials = null;
        private StorageClient _StorageClient = null;

        public string PlatformUrl { get; private set; }
        public string GoogleAuthenticationToken { get; private set; }
        public string BucketCredentials { get; private set; }
        public string PlatformAuthorization { get; private set; }
        public string UploadBucket { get; private set; }
        public string BucketPath { get; private set; }

        public int JobTimeout { get; set; } = 60;

        private GoogleCredential GetCredentials()
        {
            if (GoogleAuthenticationToken != null)
            {
                return GoogleCredential.FromAccessToken(GoogleAuthenticationToken);
            }

            if (BucketCredentials != null)
            {
                return GoogleCredential.FromFile(BucketCredentials);
            }

            return GoogleCredential.GetApplicationDefault();
        }

        async public Task<InferenceResult> Process(byte[] inImageBytes)
        {
            var lBucketFileName = $"{BucketPath}/{Guid.NewGuid().ToString()}.png";

            using (var lImageStream = new MemoryStream(inImageBytes))
            {
                var lImageObject = await _StorageClient.UploadObjectAsync(
                    bucket: UploadBucket,
                    objectName: lBucketFileName,
                    contentType: "image/png",
                    source: lImageStream,
                    options: new UploadObjectOptions { PredefinedAcl = PredefinedObjectAcl.AuthenticatedRead }
                    );
            }


            var lRequest = new Dictionary<string, string>
                {
                    {"data", $"gs://{UploadBucket}/" + lBucketFileName},
                    {"dataType", "url" }
                };

            var content = new StringContent(JsonConvert.SerializeObject(new List<Dictionary<string, string>>() { lRequest }),
                Encoding.UTF8, "application/json");

            var lHttpClient = new HttpClient();

            if (PlatformAuthorization != null)
                lHttpClient.DefaultRequestHeaders.Add("Authorization", PlatformAuthorization);

            var lResponseObject = await lHttpClient.PostAsync($"{PlatformUrl}", content);
            var lResponseString = await lResponseObject.Content.ReadAsStringAsync();

            if (!lResponseObject.IsSuccessStatusCode)
            {
                throw new Exception($"Error posting job in platform: {lResponseObject.StatusCode}");
            }

            var lResponse = JsonConvert.DeserializeObject<List<dynamic>>(lResponseString);
            var lPlatformJob = lResponse[0]["id"].ToString();

            // Sleep 1 second for job to be processed
            Thread.Sleep(1000);

            var lPlatformResult = Task<InferenceResult>.Factory.StartNew(new Func<InferenceResult>(() =>
            {
                var lSleptSeconds = 0;

                while (true)
                {
                    if (lSleptSeconds >= JobTimeout)
                    {
                        throw new Exception($"Timeout exceeded waiting for job {lPlatformJob}");
                    }

                    var lJobResultTask = lHttpClient.GetAsync($"{PlatformUrl}/jobs/{lPlatformJob}");
                    lJobResultTask.Wait();

                    if (!lJobResultTask.Result.IsSuccessStatusCode)
                    {
                        throw new Exception($"Error retrieving job in platform: {lResponseObject.StatusCode}");
                    }

                    var lReadDataTask = lJobResultTask.Result.Content.ReadAsStringAsync();
                    lReadDataTask.Wait();

                    var lJobStatus = JsonConvert.DeserializeObject<dynamic>(lReadDataTask.Result);

                    if (lJobStatus["status"] == "Done")
                    {
                        return new InferenceResult(lJobStatus["result"]["result"].ToString(), float.Parse(lJobStatus["result"]["score"].ToString()));
                    }
                    else if (lJobStatus["status"] == "Error")
                    {
                        throw new Exception($"Job {lPlatformJob} in error!");
                    }
                    else if (lJobStatus["status"] == "Aborted")
                    {
                        throw new Exception($"Job {lPlatformJob} aborted!");
                    }

                    Thread.Sleep(1000);

                    lSleptSeconds += 1;
                }
            }));


            return await lPlatformResult;
        }
    }
}
