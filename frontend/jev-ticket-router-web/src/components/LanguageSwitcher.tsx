import { useId, useState, type MouseEvent } from 'react';
import { useTranslation } from 'react-i18next';
import Button from '@mui/material/Button';
import Menu from '@mui/material/Menu';
import MenuItem from '@mui/material/MenuItem';
import ListItemText from '@mui/material/ListItemText';
import ListItemIcon from '@mui/material/ListItemIcon';
import Tooltip from '@mui/material/Tooltip';
import CheckIcon from '@mui/icons-material/Check';
import TranslateIcon from '@mui/icons-material/Translate';
import { LOCALES, LOCALE_LABELS } from '../i18n';
import { useLocale } from '../providers/useLocale';

/** Switches the interface language, and with it the whole page direction. */
export function LanguageSwitcher() {
  const { t } = useTranslation();
  const { locale, setLocale } = useLocale();
  const [anchor, setAnchor] = useState<HTMLElement | null>(null);
  const menuId = useId();

  const open = (event: MouseEvent<HTMLElement>) => {
    setAnchor(event.currentTarget);
  };

  const close = () => {
    setAnchor(null);
  };

  return (
    <>
      <Tooltip title={t('header.language')} describeChild>
        <Button
          size="small"
          color="inherit"
          onClick={open}
          startIcon={<TranslateIcon />}
          aria-label={t('header.language')}
          aria-haspopup="menu"
          aria-controls={anchor ? menuId : undefined}
          aria-expanded={anchor ? true : undefined}
          sx={{ minWidth: 0 }}
        >
          {LOCALE_LABELS[locale]}
        </Button>
      </Tooltip>

      <Menu id={menuId} anchorEl={anchor} open={Boolean(anchor)} onClose={close}>
        {LOCALES.map((option) => (
          <MenuItem
            key={option}
            selected={option === locale}
            onClick={() => {
              setLocale(option);
              close();
            }}
            lang={option}
          >
            <ListItemIcon sx={{ minWidth: 32 }}>
              {option === locale ? <CheckIcon fontSize="small" /> : null}
            </ListItemIcon>
            <ListItemText>{LOCALE_LABELS[option]}</ListItemText>
          </MenuItem>
        ))}
      </Menu>
    </>
  );
}
