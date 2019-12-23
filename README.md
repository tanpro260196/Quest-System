# Quest-System
- Command: /quest and /questsearch and /jobquest

# QUEST

- Setup your quests in Quest.json
- Quest have a limit of how many time each player can do that Quest and the period of time the quest will be valid.
- A random selection of quest in the quest bank will be enabled each day while the rest is disabled (player cannot redeem disabled quest).
- You can config the min and maximum number of quest available each day as well as the % chance of failure when the system randomize the quest through the config file.

- Player with questbuff.xx permission will receive a bonus of xx% upon quest completion. The maximum reward is 32767%.

# JOB QUEST

- Special quest that never expire and only accessiable as a certain class.
- Will be level-dependent. Also have hardmode check and multiplier.
- Use a seperate config file. JobQuest.json.
