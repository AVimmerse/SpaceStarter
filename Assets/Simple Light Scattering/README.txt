Hey There!
Thank you for purchasing my asset!
If you have any problems/question write me an email at piotrtplaystore@gmail.com

How to use:
1. Find Directional Light in your scene and attach to it script called "Directional Light Attachment"
2. Next, find your camera and add to it script called "Simple Light Scattering"
3. Now we have to configure just added to our camera Simple Light Scattering script:
    4. In Attachment, field put your "Directional Light Attachment" you created in step 1
    5. In Dense Fog Noise Tex click the select button on right side of the field and in search toolbar write "noise512" and select it
6. You are good to go, now play with parameters and adjust them to your needs!

Make sure your shadows settings are (Project Settings/Quality/Shadows):
Shadow Projection - Stable Fit
Shadowmask Mode - Distance Shadowmask (however my asset works with the second option too but it can produce graphical artefacts)
 
FAQ:

Q: How can I change the parameters of scattering in realtime in-game?
A: You can create a simple script with reference to your Simple Light Scattering script, in your shader you can access all scattering parameters and adjust them runtime.

Q: I notice some light bleeding when I look from specific angles.
A: This is probably because of using shadow cascaded try in your Project Settings/Quality/Shadows/Shadow Cascades select No Cascades and check if this helped. 
    Also, this can be caused by objects disappearing in far, to fix this tweak your Shadow Distance parameter.

Q: It's not working on VR.
A: My asset currently is not supporting VR rendering just as the description on Asset Store says.

Q: It's not working in LWRP/URP/HDRP.
A: My asset currently is not supporting these rendering pipelines just as the description on Asset Store says.
