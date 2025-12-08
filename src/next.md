# task

Do a thorough reivew all the services and their data flow, it seems like there is a lot of abstraction and wrapping and this has made the architecture very complex. 
We only need to keep track of the devices we found from BLE advertisment and the devices we have paired, these things should be at first idependent, but the records we use to model their data must have one shared id, the prodcut id we can extract.


## BLE data model
The BLE data model is alreayd defined in our Models folder, but it might contain more fields than we need. Review and simplify it to focus on the essential fields.

## Paired Device data model
The Paired Device data model should be defined in the Models folder as well. But it's missing some fields, use the MinimalCLI program to see what the fields should be and why they are necessary, they are mostly about the connection state and audio status. 


The services:
1. BLEService: This service handles the BLE advertising and scanning. It should be simplified if needed but we have already quite good implementation.
2. Paired DeviceService: this is were we have the most confusion in the current implemetnation, use the MinimalCLI program to get an idea of what the sercie should do. 

The ultimate goal is to be able to match the BLE devices with the paired devices using the product ID, and to keep track of the connection state and audio status of the paired devices but enrich with the BLE data.


The two services should be both running independently but they should be able to communicate with each other to match the BLE devices with the paired devices using the product ID. However the BLE advertising is very chatty and we need to make sure that the BLE service does not interfere with the Paired Device service.
Ultimaetly we want to be able to connect audio to the paired device and control the audio status, show the extra infomation from the BLE data to make a nice UI. The user might also want to disconnet to the paired device, but it's not clear if we should then revert audio to the default device or not. Or if we should pause the media or not.
Same consideration from the airpod being reomved from the ear, should we pause the media or not. 

Make a plan to implement the changes and improvements, including the necessary data models, services, and communication mechanisms. Also, consider the user experience and the behavior of the system when a paired device is disconnected or removed from the ear.

Do not implente the plan yet, just make a detailed plan with the necessary steps and considerations. 
Strufture the plan in a clear and organized manner, including any necessary diagrams, and a detailed checklist you can use to track progress during the implementation phase.