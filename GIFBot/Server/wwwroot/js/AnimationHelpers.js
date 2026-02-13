// Plays a single sound file.
function PlaySound(soundFile, volume, soundTimeOffsetMs) {
   if (soundFile != null && soundFile != "") {      
      var sound = new Audio(soundFile);
      sound.volume = volume;
      sound.loop = false;

      console.log('sound delay = ' + soundTimeOffsetMs);
      if (soundTimeOffsetMs > 0) {
         setTimeout(PlaySound, soundTimeOffsetMs, soundFile, volume, 0);
      }
      else {
         sound.play();
      }
   }
}

// Plays a single video file.
function PlayVideo(videoFile, volume, leftStr, topStr, widthStr, heightStr) {
   if (videoFile != null && videoFile != "") {
      var video = document.getElementById("videoAnimation");
      video.setAttribute("src", videoFile);

      video.volume = volume;

      video.style.display = "inline";
      //video.style.left = leftStr;
      //video.style.top = topStr;
      video.style.width = widthStr;
      video.style.height = heightStr;

      video.load();
      video.addEventListener("canplaythrough", function () {
         video.play();
      }, false);
   }
}

// Plays a single video file with looping.
function PlayVideoWithLooping(videoFile, volume, widthStr, heightStr) {
   if (videoFile != null && videoFile != "") {
      var video = document.getElementById("videoAnimation");
      video.setAttribute("src", videoFile);

      video.volume = volume;

      video.style.display = "inline";
      video.style.width = widthStr;
      video.style.height = heightStr;

      if (typeof video.loop == 'boolean') { // loop supported
         video.loop = true;
      } else { // loop property not supported
         video.addEventListener('ended', function () {
            this.currentTime = 0;
            this.play();
         }, false);
      }

      video.load();
      video.addEventListener("canplaythrough", function () {
         video.play();
      }, false);
   }
}

// Plays the designated video element with the volume.
function PlayVideoElement(videoElement, videoFile, volume) {
   var video = document.getElementById(videoElement);
   video.setAttribute("src", videoFile);
   video.volume = volume;
   video.load();
   video.addEventListener("canplaythrough", function () {
      video.play();
   }, false);
}

// Stops playing the designated video element.
function StopVideoElement(videoElement, videoFile) {
   var video = document.getElementById(videoElement);
   video.pause();
   video.setAttribute("src", "");
}

// Scrolls the logs down to the bottom.
function UpdateScroll() {
   var element = document.getElementById("uiLogContainer");
   element.scrollTop = element.scrollHeight;
}

// Copy Data from a Textbox
function CopyToClipboard(elementName) {
   /* Get the text field */
   var copyText = document.getElementById(elementName);

   /* Select the text field */
   copyText.select();
   copyText.setSelectionRange(0, 99999); /*For mobile devices*/

   /* Copy the text inside the text field */
   document.execCommand("copy");
}

// Play a string of text through the Google TTS System.
function PlayTTS(message, volume) {
   //if ('speechSynthesis' in window) {
      var msg = new SpeechSynthesisUtterance(message);
      msg.volume = volume; // 0 to 1
      window.speechSynthesis.speak(msg);
   //}
}

// Scrolls to the top of the page. Used by the AnimationsEditor.
function ScrollToTop() {
   window.scrollTo(0, 0);
} 