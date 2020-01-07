The **docBrain SDK for .NET 4.5** makes it easier for you to integrate your docBrain pipeline in your existing .NET solutions.

## Compilation
Source code compilation is done by cloning and compiling project docBrain.<br>
Already compiled binaries can be downloaded from binaries directory.
## Dependencies
- .NET 4.5
- Nuget package Google.Cloud.Storage.V1
## Usage
```
// use docBrain namespace
using docBrain;

// initialize client
var lClient = new DocbrainClient(inPlatformUrl: @"https://example.docbrain.ai/public/services/handwritten", // pipeline service url
                                 inPlatformAuthorization: "", // platform authorization token
                                 inBucketCredentials: "data-writer.json" // service account credentials used to connect to target bucket
                                 );
// Test image path
var lImagePath = @"test_sample.png";

using (Image image = Image.FromFile(lImagePath))
{
    using (var lStream = new MemoryStream())
    {
        image.Save(lStream, image.RawFormat);
        var lImageBytes = lStream.ToArray();
        
        var lProcessResult = await lClient.Process(lImageBytes);
        
        Console.WriteLine($"{lProcessResult.Result}: {lProcessResult.Score}");
    }
}

```
