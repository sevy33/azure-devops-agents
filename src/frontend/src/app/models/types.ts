export interface Connection {
  id: string;
  displayName: string;
  organizationUrl: string;
  projectName: string;
  createdAt: string;
  isAuthenticated: boolean;
}

export interface RepoInfo {
  id: string;
  repoName: string;
  remoteUrl: string;
  cloneStatus: 'Pending' | 'Cloning' | 'Ready' | 'Failed';
  clonedAt: string | null;
}

export interface ChatSession {
  id: string;
  connectionId: string;
  title: string | null;
  createdAt: string;
}

export interface ChatMessage {
  id: string;
  role: 'User' | 'Assistant' | 'System';
  content: string;
  createdAt: string;
}

export interface AgentJob {
  id: string;
  agentType: 'Developer' | 'Analyst';
  status: 'Queued' | 'Running' | 'Succeeded' | 'Failed' | 'Cancelled';
  workItemId: string | null;
  workItemTitle: string | null;
  resultUrl: string | null;
  errorMessage: string | null;
  createdAt: string;
  updatedAt: string;
}

// SignalR event shapes
export interface MessageChunkEvent {
  sessionId: string;
  messageId: string;
  chunk: string;
  isFinal: boolean;
}

export interface AgentStatusEvent {
  sessionId: string;
  jobId: string;
  agentType: string;
  status: string;
  detail: string | null;
}

export interface AgentLogEvent {
  jobId: string;
  line: string;
}
