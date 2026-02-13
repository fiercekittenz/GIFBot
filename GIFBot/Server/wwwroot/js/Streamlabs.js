///<summary>
/// Setup the Streamlabs websocket (socket.io) connection for listening.
///</summary>

var streamlabsToken = null;
var streamlabsSocket = null;
var streamlabsConnected = false;
var streamlabsConnectionAttemptCount = 0;
var streamlabsMaxConnectionsReached = false;

function SetupStreamlabs(socketToken) {
   if ((socketToken && socketToken !== "" && streamlabsToken != socketToken) || streamlabsConnected === false) {
      // Max connection attempts reached. Stop trying and warn the user only once.
      if (streamlabsConnectionAttemptCount > 4) {
         if (streamlabsMaxConnectionsReached === false) {
            // Only log and warn the user once.
            console.log("Streamlabs max connection attempts reached.");
            var xhr = createCORSRequest('GET', "http://localhost:5000/streamlabs/maxconnectionattempts");
            if (!xhr) {
               throw new Error('CORS not supported!');
            }
            xhr.send();
         }

         streamlabsMaxConnectionsReached = true;

         if (streamlabsToken != socketToken) {
            // We have a new token. Wipe connection attempts and retry.
            console.log("New Streamlabs token received. Restarting connection attempts.");
            streamlabsMaxConnectionsReached = false;
            streamlabsConnectionAttemptCount = 0;
         }
         else {
            // No new token. Just exit.
            return;
         }
      }

      streamlabsToken = socketToken;
      if (streamlabsToken !== "") {
         var streamlabsURL = "wss://sockets.streamlabs.com?token=" + streamlabsToken + "&EIO=3&transport=websocket";
         streamlabsSocket = io(streamlabsURL, { transports: ['websocket'] });
         streamlabsSocket.on('event', (eventData) => {
            if (eventData.type === 'donation' && eventData.message[0] !== 'undefined') {
               console.log(eventData);
               var donation = eventData.message[0];
               var messageForGIFBot = "http://localhost:5000/streamlabs/tip?amount=" + donation.amount + "&formatted_amount=" + donation.formatted_amount + "&from=" + donation.from + "&message=" + donation.message + "&eventid=" + eventData.event_id;

               var xhr = createCORSRequest('GET', messageForGIFBot);
               if (!xhr) {
                  throw new Error('CORS not supported!');
               }

               xhr.onload = function () {
                  var responseText = xhr.responseText;
               };

               xhr.onerror = function () {
                  console.log('GIFBot Web Service for Streamlabs endpoint is not running!');
               };

               xhr.send();
            }
            else {
               console.log("Event triggered: " + eventData.type);
            }
         });

         streamlabsSocket.on('connect_failed', () => {
            console.log("Streamlabs connection failed.");
            streamlabsConnected = false;
            ++streamlabsConnectionAttemptCount;
         });

         streamlabsSocket.on('connect', () => {
            // For some reason, the socket connection to SL is wonky and notifies of a 
            // successful connection every dang time. Just ignore it if we're already connected.
            if (streamlabsConnected === false) {
               console.log("Streamlabs connected!");
               streamlabsConnected = true;

               var xhr = createCORSRequest('GET', "http://localhost:5000/streamlabs/connected");
               if (!xhr) {
                  throw new Error('CORS not supported!');
               }
               xhr.send();
            }
         });

         streamlabsSocket.on('disconnect', () => {
            console.log("Streamlabs disconnected.");
            streamlabsConnected = false;
            ++streamlabsConnectionAttemptCount;

            var xhr = createCORSRequest('GET', "http://localhost:5000/streamlabs/disconnected");
            if (!xhr) {
               throw new Error('CORS not supported!');
            }
            xhr.send();
         });

         streamlabsSocket.on('connect_error', (error) => {
            console.log("Streamlabs connection error: " + error);
            streamlabsConnected = false;
            ++streamlabsConnectionAttemptCount;

            var xhr = createCORSRequest('GET', "http://localhost:5000/streamlabs/connectionerror");
            if (!xhr) {
               throw new Error('CORS not supported!');
            }
            xhr.send();
         });

         streamlabsSocket.on('error', (error) => {
            console.log("Streamlabs error: " + error);
            streamlabsConnected = false;
            ++streamlabsConnectionAttemptCount;

            var xhr = createCORSRequest('GET', "http://localhost:5000/streamlabs/connectionerror");
            if (!xhr) {
               throw new Error('CORS not supported!');
            }
            xhr.send();
         });
      }
   }
}
