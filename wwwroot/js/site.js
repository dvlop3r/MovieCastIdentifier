// Please see documentation at https://docs.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

document.addEventListener("DOMContentLoaded", () => {
    // <snippet_Connection>
    const connection = new signalR.HubConnectionBuilder()
        .withUrl("/fileStreamHub")
        .withAutomaticReconnect()
        .build();
    // </snippet_Connection>

    // <snippet_ReceiveMessage>
    connection.on("ReceiveMessage", (user, message) => {
        $("#message").text(message);
    });
    // </snippet_ReceiveMessage>

    // document.getElementById("send").addEventListener("click", async () => {
    //     const user = document.getElementById("userInput").value;
    //     const message = document.getElementById("messageInput").value;

    //     // <snippet_Invoke>
    //     try {
    //         await connection.invoke("SendMessage", user, message);
    //     } catch (err) {
    //         console.error(err);
    //     }
    //     // </snippet_Invoke>
    // });


    async function start() {
        try {
            await connection.start();
            console.log("SignalR Connected.");
        } catch (err) {
            console.log(err);
            setTimeout(start, 5000);
        }
    };

    connection.onclose(async () => {
        await start();
    });

    // Start the connection.
    start();
});