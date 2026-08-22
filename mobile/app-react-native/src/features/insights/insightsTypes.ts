export type AnalyticsPeriodSummary = {
  periodStart: string;
  periodEnd: string;
  incomeTotal: number;
  expenseTotal: number;
  balanceDelta: number;
};

export type LimitProgress = {
  periodStart: string;
  periodEnd: string;
  isConfigured: boolean;
  limit: number | null;
  spent: number;
  remaining: number | null;
  usedPercent: number | null;
};

export type AnalyticsDashboard = {
  currency: string;
  timeZoneId: string;
  referenceDate: string;
  dailySummary: AnalyticsPeriodSummary;
  weeklySummary: AnalyticsPeriodSummary;
  monthlySummary: AnalyticsPeriodSummary;
  dailyLimit: {
    isConfigured: boolean;
    limit: number | null;
    spent: number;
    remaining: number | null;
    usedPercent: number | null;
  };
  limitsProgress: {
    daily: LimitProgress;
    weekly: LimitProgress;
    monthly: LimitProgress;
    trackingStreak: {
      currentDays: number;
      lastTrackedDate: string | null;
      message: string;
    };
  };
  monthlyProgress: {
    incomeTotal: number;
    expenseTotal: number;
    balanceDelta: number;
    expenseToIncomePercent: number | null;
  };
  freshness: {
    isStale: boolean;
    lastEventAtUtc: string | null;
  };
};

export type FinancialScore = {
  calculationId: string;
  currency: string;
  score: number;
  formulaVersion: string;
  factors: {
    code: string;
    contribution: number;
    explanation: string;
    inputs: { code: string; value: number; unit: string }[];
  }[];
  calculatedAtUtc: string;
};

export type Recommendation = {
  recommendationId: string;
  currency: string;
  code: string;
  severity: string;
  title: string;
  body: string;
  explanation: {
    localizationKey: string;
    text: string;
    confidence: string;
    action: { code: string; route: string };
    isWordingEnhanced: boolean;
  };
  facts: { code: string; value: number }[];
  generatedAtUtc: string;
  status: string;
  statusChangedAtUtc: string;
};

export type RecommendationList = {
  currency: string;
  items: Recommendation[];
};

export type UserProfile = {
  userId: string;
  locale: string;
  timeZone: string;
  currencyCode: string;
  privacyMode: 'standard' | 'strict';
  aiPersonalizationEnabled: boolean;
  firstDayOfWeek: string;
  monthlyBudgetAmount: number;
  budgetNotificationsEnabled: boolean;
  weeklySummaryNotificationsEnabled: boolean;
  profileOnboardingCompleted: boolean;
  preferencesOnboardingCompleted: boolean;
  createdAtUtc: string;
  updatedAtUtc: string;
};

export type ProfileUpdate = {
  locale: string;
  timeZone: string;
  currencyCode: string;
  privacyMode: 'standard' | 'strict';
  aiPersonalizationEnabled: boolean;
  firstDayOfWeek: string;
  monthlyBudgetAmount: number;
  budgetNotificationsEnabled: boolean;
  weeklySummaryNotificationsEnabled: boolean;
  profileOnboardingCompleted: boolean;
  preferencesOnboardingCompleted: boolean;
};

export type NotificationPreferences = {
  pushEnabled: boolean;
  webEnabled: boolean;
  enabledNotificationTypes: string[];
  quietHours: {
    startsAt: string;
    endsAt: string;
    timeZoneId: string;
  } | null;
};
