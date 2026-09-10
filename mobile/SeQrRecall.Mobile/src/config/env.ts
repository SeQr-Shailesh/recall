export type AppEnvironment = 'development' | 'staging' | 'production';

export interface AppConfig {
  environment: AppEnvironment;
  apiBaseUrl: string;
  apiVersionPrefix: string;
}

const DEVELOPMENT_CONFIG: AppConfig = {
  environment: 'development',
  apiBaseUrl: 'http://10.0.2.2:5080',
  apiVersionPrefix: '/api/v1',
};

const STAGING_CONFIG: AppConfig = {
  environment: 'staging',
  apiBaseUrl: 'https://staging.example.com',
  apiVersionPrefix: '/api/v1',
};

const PRODUCTION_CONFIG: AppConfig = {
  environment: 'production',
  apiBaseUrl: 'https://recall-api.seqrtechnology.com',
  apiVersionPrefix: '/api/v1',
};

function assertProductionUsesHttps(config: AppConfig): void {
  if (config.environment === 'production' && !config.apiBaseUrl.startsWith('https://')) {
    throw new Error('Production API configuration must use HTTPS.');
  }
}

export function getAppConfig(environment: AppEnvironment = 'development'): AppConfig {
  const config =
    environment === 'production'
      ? PRODUCTION_CONFIG
      : environment === 'staging'
        ? STAGING_CONFIG
        : DEVELOPMENT_CONFIG;

  assertProductionUsesHttps(config);
  return config;
}

export const appConfig = getAppConfig('production');
