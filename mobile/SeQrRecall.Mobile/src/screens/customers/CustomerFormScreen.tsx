import { useCallback, useEffect, useState } from 'react';
import { ActivityIndicator, Image, Pressable, Text, View } from 'react-native';
import { Screen } from '@components/Screen';
import { PrimaryButton } from '@components/PrimaryButton';
import { TextField } from '@components/TextField';
import * as customersApi from '@api/customersApi';
import { choosePhoto } from '@services/photoPicker';
import { useCustomersStore } from '@store/customersStore';
import { useTheme } from '@theme/useTheme';
import { getErrorMessage } from '@utils/errors';
import type { PhotoUpload } from '@api/customersApi';
import type { NativeStackScreenProps } from '@react-navigation/native-stack';
import type { CustomersStackParamList } from '@navigation/types';

type Props = NativeStackScreenProps<CustomersStackParamList, 'CustomerForm'>;

export function CustomerFormScreen({ navigation, route }: Props) {
  const theme = useTheme();
  const upsert = useCustomersStore((state) => state.upsert);
  const customerId = route.params.customerId;
  const isEdit = Boolean(customerId);

  const [name, setName] = useState('');
  const [companyName, setCompanyName] = useState('');
  const [mobile, setMobile] = useState('');
  const [email, setEmail] = useState('');
  const [photo, setPhoto] = useState<PhotoUpload | null>(null);
  const [existingPhotoUri, setExistingPhotoUri] = useState<string | null>(null);
  const [loading, setLoading] = useState(isEdit);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    if (!customerId) {
      return;
    }

    try {
      setError(null);
      const customer = await customersApi.getCustomer(customerId);
      setName(customer.name);
      setCompanyName(customer.companyName ?? '');
      setMobile(customer.mobile ?? '');
      setEmail(customer.email ?? '');
      if (customer.hasPhoto) {
        const uri = await customersApi.getCustomerPhotoDataUri(customerId);
        setExistingPhotoUri(uri);
      }
    } catch (caught) {
      setError(getErrorMessage(caught, 'Unable to open this customer.'));
    } finally {
      setLoading(false);
    }
  }, [customerId]);

  useEffect(() => {
    void load();
  }, [load]);

  async function onPickPhoto() {
    try {
      const picked = await choosePhoto('Customer photo');
      if (picked) {
        setPhoto(picked);
        setExistingPhotoUri(null);
      }
    } catch (caught) {
      setError(getErrorMessage(caught, 'Unable to add a photo.'));
    }
  }

  async function onSave() {
    const trimmedName = name.trim();
    if (!trimmedName) {
      setError('Customer name is required.');
      return;
    }

    setSaving(true);
    try {
      setError(null);
      const fields = {
        name: trimmedName,
        companyName,
        mobile,
        email,
      };
      const saved = isEdit && customerId
        ? await customersApi.updateCustomer(customerId, fields)
        : await customersApi.createCustomer(fields);
      const withPhoto = photo ? await customersApi.uploadCustomerPhoto(saved.id, photo) : saved;
      upsert(withPhoto);
      if (isEdit) {
        navigation.goBack();
      } else {
        navigation.replace('CustomerDetails', { customerId: withPhoto.id });
      }
    } catch (caught) {
      setError(getErrorMessage(caught, 'Unable to save this customer.'));
    } finally {
      setSaving(false);
    }
  }

  if (loading) {
    return (
      <Screen scroll={false}>
        <ActivityIndicator color={theme.colors.primary} />
      </Screen>
    );
  }

  const previewUri = photo?.uri ?? existingPhotoUri;

  return (
    <Screen>
      <Pressable onPress={() => navigation.goBack()}>
        <Text style={[theme.typography.body, { color: theme.colors.primary, fontWeight: '600' }]}>
          Back
        </Text>
      </Pressable>
      <Text style={[theme.typography.title, { color: theme.colors.text, marginTop: 12, marginBottom: 20 }]}>
        {isEdit ? 'Edit customer' : 'Add customer'}
      </Text>
      {error ? (
        <Text style={[theme.typography.body, { color: theme.colors.danger, marginBottom: 12 }]}>{error}</Text>
      ) : null}
      <TextField
        label="Name"
        value={name}
        onChangeText={setName}
        placeholder="Full name"
        autoCapitalize="words"
        autoComplete="name"
        maxLength={200}
      />
      <TextField
        label="Company"
        value={companyName}
        onChangeText={setCompanyName}
        placeholder="Optional"
        autoCapitalize="words"
        maxLength={200}
      />
      <TextField
        label="Mobile"
        value={mobile}
        onChangeText={setMobile}
        placeholder="Optional"
        keyboardType="phone-pad"
        autoComplete="tel"
        maxLength={20}
      />
      <TextField
        label="Email"
        value={email}
        onChangeText={setEmail}
        placeholder="Optional"
        keyboardType="email-address"
        autoComplete="email"
        maxLength={256}
      />
      {previewUri ? (
        <Image
          source={{ uri: previewUri }}
          style={{ width: 120, height: 120, borderRadius: 12, marginBottom: 12, backgroundColor: theme.colors.border }}
        />
      ) : null}
      <Pressable onPress={() => void onPickPhoto()} style={{ marginBottom: 24 }}>
        <Text style={[theme.typography.body, { color: theme.colors.primary, fontWeight: '600' }]}>
          {previewUri ? 'Change photo' : 'Add photo'}
        </Text>
      </Pressable>
      <PrimaryButton
        label={isEdit ? 'Save changes' : 'Save customer'}
        onPress={() => void onSave()}
        loading={saving}
      />
      <View style={{ height: 24 }} />
    </Screen>
  );
}
