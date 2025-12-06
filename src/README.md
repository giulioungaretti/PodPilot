Run git.ps1 to set the username! 


## UI views 
Expand on this plan: 

The application should open and show the saved airpods if they exist in a promiment box, otherwise a palceholder text. 
Below a list of devices that found in the advertisement, oterwhise show an empty page that says "open your aiprods case to discover".

TODO: do they need to be paired frirst or can we connect directly? 

The card for the airpods should show all the informations, such as battery level, connection status, and any other relevant details.
Use emojis as much as possilbe and keep the rest simple.
Use as much transparent acrylic materials as possible for the UI elements. Otherwise follow this: https://learn.microsoft.com/en-us/windows/apps/design/signature-experiences/materials 

Use mvvm tooklit to model the data and the viewmodels. 

Keep the mainwindow simple and implement this in mainpage, ideally use reusalbe compoments for the device cards so that we can build in the future a smaller view for the airpods case that can be used in a separate window or page or navigated to. 

Write simple tests for the viewmodels and the UI elements to ensure they work as expected. Mock the data for the tests to simulate different scenarios, such as no devices found, devices with low battery, and paired devices.