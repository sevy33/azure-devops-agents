I want to create an ai agent app kind of like openclaw, but it works with Azure Devops. I am just brainstorming and would love some input. I think the dashboard should allow you to add an Azure Devops repo, once added it could show a list of repos on the left side of the screen. If I select one, in the main view should show 3 Agents (Assistant, Developer, and Analyst). The Assistant agent should be able to tell me what work items I should work on next or what items need more attention so AI can handle the work itme. The developer agent should be able to start coding task for the repository for a specific workitem. If the item is to hard or cant be completed, it will need to be able to add a comment to the work item so the assistant agent can let me know what needs to be changed/updated. The developer should be able to go through and complete the item and then make a pull request. The analyst agent, I am not really concerned with right now but it should be able to get reporting data about work items and pull requests. I don't want to worry about the analyst right now, but maybe in the future. When a specific agent is selected, I should be able to chat with it. 

Or maybe I only want to talk to the Assistant agent, and the assistant can use sub-agents to handle delegating tasks to the developer agent and getting reports from the analyst agent. I think I may like this idea better, but would like your input.

Technologies to use
Github Copilot SDK
Github Copilot CLI
Microsoft azure-devops-mcp
Angular version 21 zoneless for the frontend
dotnet version 10 for backend
if you need a database, use sql lite right now and we can change it later

Things to consider
I am fine if all the repo's would have to be on the server so the copilot cli could access for the developer agent

Here are some urls for context
#fetch
https://github.com/microsoft/azure-devops-mcp

https://github.com/github/copilot-sdk

https://docs.github.com/en/copilot/how-tos/copilot-cli/use-copilot-cli

