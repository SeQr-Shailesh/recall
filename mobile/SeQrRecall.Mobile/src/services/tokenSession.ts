let accessToken: string | null = null;

export const tokenSession = {
  getAccessToken(): string | null {
    return accessToken;
  },

  setAccessToken(token: string | null): void {
    accessToken = token;
  },

  clear(): void {
    accessToken = null;
  },
};
