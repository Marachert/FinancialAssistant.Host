import { StyleSheet } from 'react-native';

export const theme = {
  colors: {
    canvas: '#F6F7F9',
    surface: '#FFFFFF',
    surfaceSubtle: '#EEF1F4',
    textPrimary: '#18202A',
    textSecondary: '#5C6773',
    border: '#C9D0D8',
    action: '#0B6E69',
    actionPressed: '#075854',
    info: '#2156A5',
    positive: '#197A45',
    warning: '#8A5200',
    critical: '#B42318',
    onAction: '#FFFFFF',
  },
  spacing: { xs: 4, sm: 8, md: 12, lg: 16, xl: 24, xxl: 32 },
  radius: { control: 8 },
} as const;

export const typography = StyleSheet.create({
  title: { fontSize: 24, lineHeight: 32, fontWeight: '700' },
  heading: { fontSize: 20, lineHeight: 28, fontWeight: '600' },
  body: { fontSize: 16, lineHeight: 24, fontWeight: '400' },
  bodyStrong: { fontSize: 16, lineHeight: 24, fontWeight: '600' },
  small: { fontSize: 14, lineHeight: 20, fontWeight: '400' },
});
