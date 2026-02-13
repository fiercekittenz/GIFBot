function PlaceElement(element, width, height, top, left) {
   if (document.getElementById(element)) {
      document.getElementById(element).style.width = width + "px";
      document.getElementById(element).style.height = height + "px";
      document.getElementById(element).style.top = top + "px";
      document.getElementById(element).style.left = left + "px";
   }
   else {
      console.log("Couldn't find document element '" + element + "'");
   }
}

function SetupElementDrag(elementName, parentElementName, scalar, visualtype) {

   if (document.getElementById(elementName)) {
      // If present, the header is where you move the DIV from:
      $("#" + elementName).draggable({ containment: "#" + parentElementName });

      // Setup mouse up on this element so it can be captured.
      var elementDiv = document.getElementById(elementName);
      if (elementDiv != null) {
         elementDiv.addEventListener('mouseup', function () {
            updateDisplay(elementDiv, scalar, visualtype, true);
         });
         elementDiv.addEventListener('mousemove', function () {
            updateDisplay(elementDiv, scalar, visualtype, false);
         });
      }
   }
   else {
      console.log("Couldn't find document element '" + elementName + "'");
      return;
   }
}

function SetupElementDragWithNoContainment(elementName, scalar, visualtype) {

   if (document.getElementById(elementName)) {
      // If present, the header is where you move the DIV from:
      $("#" + elementName).draggable("option", "containment", "document");

      // Setup mouse up on this element so it can be captured.
      var elementDiv = document.getElementById(elementName);
      if (elementDiv != null) {
         elementDiv.addEventListener('mouseup', function () {
            updateDisplay(elementDiv, scalar, visualtype, true);
         });
         elementDiv.addEventListener('mousemove', function () {
            updateDisplay(elementDiv, scalar, visualtype, false);
         });
      }
   }
   else {
      console.log("Couldn't find document element '" + elementName + "'");
      return;
   }
}

function GetElementTop(elementName) {
   if (document.getElementById(elementName)) {
      return document.getElementById(elementName).style.top;
   }

   return 0;
}

function GetElementLeft(elementName) {
   if (document.getElementById(elementName)) {
      return document.getElementById(elementName).style.left;
   }

   return 0;
}

function ForceUpdateDisplay(elementName, scalar, visualtype) {

   var elementDiv = document.getElementById(elementName);
   if (elementDiv != null) {
      updateDisplay(elementDiv, scalar, visualtype, true);
   }
}

function updateDisplay(elementDiv, scalar, visualtype, updateclient) {
   var top = elementDiv.style.top;
   var left = elementDiv.style.left;

   if (elementDiv != null && scalar != null) {

      var xhr = createCORSRequest('GET', "http://localhost:5000/utility/updatevisual?visualType=" + visualtype + "&top=" + top + "&left=" + left + "&scalar=" + scalar + "&updateclient=" + updateclient);
      if (!xhr) {
         console.log('CORS not supported!');
         throw new Error('CORS not supported!');
      }

      xhr.onload = function () {
      };

      xhr.onerror = function () {
         console.log('GIFBot Web Service is not running!');
      };

      xhr.send();
   }
}