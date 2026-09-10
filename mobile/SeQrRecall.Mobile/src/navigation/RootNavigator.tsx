import { useMemo } from 'react';
import { ActivityIndicator, View } from 'react-native';
import { NavigationContainer } from '@react-navigation/native';
import { createNativeStackNavigator } from '@react-navigation/native-stack';
import { createBottomTabNavigator } from '@react-navigation/bottom-tabs';
import { IdentifierScreen } from '@screens/auth/IdentifierScreen';
import { OtpScreen } from '@screens/auth/OtpScreen';
import { ProfileSetupScreen } from '@screens/auth/ProfileSetupScreen';
import { NotesListScreen } from '@screens/notes/NotesListScreen';
import { RecordScreen } from '@screens/notes/RecordScreen';
import { NoteDetailsScreen } from '@screens/notes/NoteDetailsScreen';
import { LeadsListScreen } from '@screens/leads/LeadsListScreen';
import { RecordLeadScreen } from '@screens/leads/RecordLeadScreen';
import { LeadDetailsScreen } from '@screens/leads/LeadDetailsScreen';
import { CustomersListScreen } from '@screens/customers/CustomersListScreen';
import { CustomerFormScreen } from '@screens/customers/CustomerFormScreen';
import { CustomerDetailsScreen } from '@screens/customers/CustomerDetailsScreen';
import { RecordInteractionScreen } from '@screens/customers/RecordInteractionScreen';
import { InteractionDetailsScreen } from '@screens/customers/InteractionDetailsScreen';
import { TabIcon } from '@components/TabIcon';
import { useAuthStore } from '@store/authStore';
import { useTheme } from '@theme/useTheme';
import customersIcon from '@assets/icons/customers.png';
import leadsIcon from '@assets/icons/leads.png';
import notesIcon from '@assets/icons/notes.png';
import type { BottomTabNavigationOptions } from '@react-navigation/bottom-tabs';
import type {
  AuthStackParamList,
  CustomersStackParamList,
  LeadsStackParamList,
  NotesStackParamList,
  RootTabParamList,
} from '@navigation/types';

const AuthStack = createNativeStackNavigator<AuthStackParamList>();
const NotesStack = createNativeStackNavigator<NotesStackParamList>();
const LeadsStack = createNativeStackNavigator<LeadsStackParamList>();
const CustomersStack = createNativeStackNavigator<CustomersStackParamList>();
const Tab = createBottomTabNavigator<RootTabParamList>();

function tabOptions(title: string, icon: number): BottomTabNavigationOptions {
  return {
    title,
    tabBarIcon: ({ color, size }) => <TabIcon source={icon} color={color} size={size} />,
  };
}

const NOTES_TAB_OPTIONS = tabOptions('Notes', notesIcon);
const LEADS_TAB_OPTIONS = tabOptions('Leads', leadsIcon);
const CUSTOMERS_TAB_OPTIONS = tabOptions('Customers', customersIcon);

function AuthNavigator({ needsProfile }: { needsProfile: boolean }) {
  return (
    <AuthStack.Navigator id="Auth" screenOptions={{ headerShown: false }}>
      {needsProfile ? (
        <AuthStack.Screen name="ProfileSetup" component={ProfileSetupScreen} />
      ) : (
        <>
          <AuthStack.Screen name="Identifier" component={IdentifierScreen} />
          <AuthStack.Screen name="Otp" component={OtpScreen} />
          <AuthStack.Screen name="ProfileSetup" component={ProfileSetupScreen} />
        </>
      )}
    </AuthStack.Navigator>
  );
}

function NotesNavigator() {
  return (
    <NotesStack.Navigator id="Notes" screenOptions={{ headerShown: false }}>
      <NotesStack.Screen name="NotesList" component={NotesListScreen} />
      <NotesStack.Screen name="Record" component={RecordScreen} />
      <NotesStack.Screen name="NoteDetails" component={NoteDetailsScreen} />
    </NotesStack.Navigator>
  );
}

function LeadsNavigator() {
  return (
    <LeadsStack.Navigator id="Leads" screenOptions={{ headerShown: false }}>
      <LeadsStack.Screen name="LeadsList" component={LeadsListScreen} />
      <LeadsStack.Screen name="RecordLead" component={RecordLeadScreen} />
      <LeadsStack.Screen name="LeadDetails" component={LeadDetailsScreen} />
    </LeadsStack.Navigator>
  );
}

function CustomersNavigator() {
  return (
    <CustomersStack.Navigator id="Customers" screenOptions={{ headerShown: false }}>
      <CustomersStack.Screen name="CustomersList" component={CustomersListScreen} />
      <CustomersStack.Screen name="CustomerForm" component={CustomerFormScreen} />
      <CustomersStack.Screen name="CustomerDetails" component={CustomerDetailsScreen} />
      <CustomersStack.Screen name="RecordInteraction" component={RecordInteractionScreen} />
      <CustomersStack.Screen name="InteractionDetails" component={InteractionDetailsScreen} />
    </CustomersStack.Navigator>
  );
}

function AppNavigator() {
  const theme = useTheme();
  const screenOptions = useMemo(
    () => ({
      headerShown: false,
      tabBarActiveTintColor: theme.colors.primary,
      tabBarInactiveTintColor: theme.colors.mutedText,
      tabBarLabelStyle: { fontWeight: '600' as const, fontSize: 12 },
      tabBarStyle: {
        backgroundColor: theme.colors.surface,
        borderTopColor: theme.colors.border,
      },
    }),
    [theme.colors.border, theme.colors.mutedText, theme.colors.primary, theme.colors.surface],
  );

  return (
    <Tab.Navigator id="AppTabs" screenOptions={screenOptions}>
      <Tab.Screen name="NotesTab" component={NotesNavigator} options={NOTES_TAB_OPTIONS} />
      <Tab.Screen name="LeadsTab" component={LeadsNavigator} options={LEADS_TAB_OPTIONS} />
      <Tab.Screen name="CustomersTab" component={CustomersNavigator} options={CUSTOMERS_TAB_OPTIONS} />
    </Tab.Navigator>
  );
}

export function RootNavigator() {
  const theme = useTheme();
  const isReady = useAuthStore((state) => state.isReady);
  const user = useAuthStore((state) => state.user);

  if (!isReady) {
    return (
      <View
        style={{
          flex: 1,
          alignItems: 'center',
          justifyContent: 'center',
          backgroundColor: theme.colors.background,
        }}
      >
        <ActivityIndicator color={theme.colors.primary} />
      </View>
    );
  }

  const signedIn = user !== null;
  const needsProfile = signedIn && !user.isProfileComplete;

  return (
    <NavigationContainer>
      {signedIn && !needsProfile ? <AppNavigator /> : <AuthNavigator needsProfile={needsProfile} />}
    </NavigationContainer>
  );
}
