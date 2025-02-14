﻿using System;
using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;

/// <summary>
/// This class handles all the network requests and serialization/deserialization of data
/// <summary>
public class NetworkManager : MonoBehaviour
{
    // reference to BotUI class
    public BotUI botUI;

    // Unity communicates to Rasa using custom connectors and POST requests.
    // Rasa implement a default rest connector which can be accessed at rasa_url
    // NOTE: on next time you start ngrok docker container, you will need to update hostname
    private const string rasa_url = "http://localhost:5005/webhooks/rest/webhook";

    /// <summary>
    /// This method is called when user has entered their message and hits the send
    /// button. It calls the <see cref="NetworkManager.PostRequest(string, string)">
    /// <summary>
    // Will be called when user presses the send button
    public void SendMessageToRasa()
    {
        if(botUI != null)
        {
        Debug.Log("botUI is enabled");
        }
        else
        {
        Debug.Log("Couldn't find a reference to BotUI in NetworkManager");
        }

        // get user message from input field, create a json object
        // from user message and then clear input field
        string message = botUI.input.text;
        botUI.input.text = "";

        // Create a json object from user messaage
        PostMessage postMessage = new PostMessage
        {
            sender = "User",
            message = message
        };

        string jsonBody = JsonUtility.ToJson(postMessage);
        print("User json: " + jsonBody);
        print("User message: " + message);

        // Update UI object with user message
        botUI.UpdateDisplay("user", message, "text");

        // Create a post request with the data to send to Rasa server
        StartCoroutine(PostRequest(rasa_url, jsonBody));

        //edw stop

    }

    /// <summary>
    /// This is a coroutine to asynchronously send a POST request to the Rasa server with
    /// the user message. The response is deserialized and rendered on the UI object.
    /// <summary>
    /// <param name="url">the url where Rasa server is hosted</param>
    /// <param name="jsonBody">user message serialized into a json object</param>
    /// <returns></returns>
    private IEnumerator PostRequest (string url, string jsonBody)
    {
        // Create a request to hit the rasa custom connector
        // Create a request to hit the rasa custom connector
        UnityWebRequest request = new UnityWebRequest(url, "POST");
        byte[] rawBody = new System.Text.UTF8Encoding().GetBytes(jsonBody);
        request.uploadHandler = (UploadHandler)new UploadHandlerRaw(rawBody);
        request.downloadHandler = (DownloadHandler)new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");

        // receive the response
        yield return request.SendWebRequest();

        // Render the response on UI object
        Debug.Log("Response: " + request.downloadHandler.text);
        ReceiveMessage(request.downloadHandler.text);
    }

    /// <summary>
    /// This method updates the UI object with bot response
    /// <summary>
    /// <param name="response">response json received from the bot</param>
    public void ReceiveMessage(string response) {
        // Deserialize response received from the bot
        RootMessages receiveMessages = JsonUtility.FromJson<RootMessages>("{\"messages\":" + response + "}");

        // show message based on message type on UI
        foreach(ReceiveData message in receiveMessages.messages) {
            FieldInfo[] fields = typeof(ReceiveData).GetFields();
            foreach(FieldInfo field in fields) {
                string data = null;

                // extract data from response in try-catch for handling null exceptions
                try {
                    data = field.GetValue(message).ToString();
                    // Edw ksekiname ti metafrash pou erxetai apo apantisi tou rasa chat. Bazoume tin apantisi
                    // sto pedio kai ginetai to synthesizer.
                    GameObject.Find("Canvas").GetComponent<HelloWorld>().inputField.text = data;
                    GameObject.Find("Canvas").GetComponent<HelloWorld>().ButtonClick();

                }
                catch(NullReferenceException) { }

                // print data
                if(data != null && field.Name != "recipient_id") {
                    botUI.UpdateDisplay("bot", data, field.Name);
                }
            }
        }
    }


    /// <summary>
    /// This method gets url resource from link and applies it to the passed texture
    /// <summary>
    /// <param name="url">url where the image resource is located</param>
    /// <param name="image">RawImage object on which the texture will be applied</param>
    /// <returns></returns>
    public IEnumerator SetImageTextureFromUrl(string url, Image image) {
        // Send request to get the image resource
        UnityWebRequest request = UnityWebRequestTexture.GetTexture(url);
        yield return request.SendWebRequest();

        if(request.result == UnityWebRequest.Result.ProtocolError)
        { // Aliws sti parenthesi vazoume auto - (request.isNetworkError || request.isHttpError) -
            // image could not be retrieved
            Debug.Log(request.error);
        }
        else {
            // Create Texture2D from Texture object
            Texture texture = ((DownloadHandlerTexture)request.downloadHandler).texture;
            Texture2D texture2D = texture.ToTexture2D();

            // set max size for image width and height based on chat size limits
            float imageWidth = 0, imageHeight = 0, texWidth = texture2D.width, texHeight = texture2D.height;
            if((texture2D.width > texture2D.height) && texHeight > 0) {
                // Landscape image
                imageWidth = texWidth;

                if(imageWidth > 200) {
                    imageWidth = 200;
                }

                float ratio = texWidth/imageWidth;
                imageHeight = texHeight/ratio;
            }
            if((texture2D.width < texture2D.height) && texWidth > 0) {
                // Portrait image
                imageHeight = texHeight;

                if(imageHeight > 200) {
                    imageHeight = 200;
                }

                float ratio = texHeight/imageHeight;
                imageWidth = texWidth/ratio;
            }

            // Resize texture to chat size limits and attach to message 
            // Image object as sprite
            TextureScale.Bilinear(texture2D, (int)imageWidth, (int)imageHeight);

            image.sprite = Sprite.Create(
                texture2D,
                new Rect(0.0f, 0.0f, texture2D.width, texture2D.height),
                new Vector2(0.5f, 0.5f), 100.0f);

            // Resize and reposition all chat bubbles
            StartCoroutine(botUI.RefreshChatBubblePosition());
        }
    }
}
