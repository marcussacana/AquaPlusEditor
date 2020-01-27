

## AquaPlusEditor - v2.1
[![Build Status](https://travis-ci.org/ForumHulp/pageaddon.svg?branch=master)](http://vnx.uvnworks.com)


This tool is focused to translate the .bin files of the Utawarerumono: Itsuwari no Kamen


Support BIN, PAK, FNT

Tested With: Utawarerumono: Itsuwari no Kamen

## Notes:
The repacker to PSV/PS4 format isn't tested.

You don't need Encrypt the .sdat after decrypted, the game can read without encryption.

The FNT is unstable, I think will works only with the Utawarerumono: Itsuwari no Kamen

To the Steam version load the font from the FNT file, you need patch the executable, See [Executable Patches](#executable-patches)  
You can need edit the Font.tex inside the Data\ENG\Texture\Font.tex too.

## Executable Patches:

**Format**:
- *Modification*: "**Patch Address**" (**Default Value**, **Patched Value**)

---
**Utawarerumono - Mask of Deception [Steam]** (**1149550**):

- *Allways use FNT font*: **0xB6206** (Default: **0x74**, **0xEB**)

---
**Utawarerumono - Mask of Truth [Steam]** (**1151440**):

- *Allways use FNT font*: **0xD73BD** (Default: **0x74**, **0xEB**)

---

## Screenshot:
Original FNT:

![http://web.archive.org/web/20190304203349if_/https://track9.mixtape.moe/uuaqwc.jpg](http://web.archive.org/web/20190304203349if_/https://track9.mixtape.moe/uuaqwc.jpg)

With custom FNT:
![https://media.discordapp.net/attachments/322613128850440192/459543445988442127/unknown.png](https://media.discordapp.net/attachments/322613128850440192/459543445988442127/unknown.png)
