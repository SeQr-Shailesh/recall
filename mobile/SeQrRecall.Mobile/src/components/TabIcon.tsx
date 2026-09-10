import { Image } from 'react-native';

export function TabIcon({ source, color, size }: { source: number; color: string; size: number }) {
  return (
    <Image
      source={source}
      resizeMode="contain"
      style={{ width: size, height: size, tintColor: color }}
    />
  );
}
