# DiscordDisco
A simple bot that streams music on request or DM's a file to you.

## Commands
Command prefix is `?` or mentioning the bot
- request: Issued with a URL to a video or song will download and send it to you via DM
- play: Issued with a URL to a video or song will download and then stream it in the configured channel
- search: Scrub YouTube and Soundcloud to find some songs that match what you want to find
  - ytsearch: Only search YouTube
  - scsearch: Only search Soundcloud
- skip: Vote to skip the current song, skips based on a ratio of currently connected listeners
- list: List the current queue
- help: Display this info

## Requirements
- libsodium
- libopus
- ffmpeg
- ffprobe
- youtube-dl

## Configuring
You pass in cmdline parameters to setup the bot
> `--channel-name <Name of the voice channel to stream to>`
> `--token <Bot API token>`
> *[Optional]* `--max-requests <Number of songs queue can hold>`
> *[Optional]* `--max-files <Number of files to keep on hand>`
