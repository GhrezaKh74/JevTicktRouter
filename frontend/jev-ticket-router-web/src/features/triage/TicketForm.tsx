import { useEffect, useMemo } from 'react';
import { Controller, useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { useTranslation } from 'react-i18next';
import Card from '@mui/material/Card';
import CardContent from '@mui/material/CardContent';
import CardHeader from '@mui/material/CardHeader';
import TextField from '@mui/material/TextField';
import MenuItem from '@mui/material/MenuItem';
import Button from '@mui/material/Button';
import Stack from '@mui/material/Stack';
import Divider from '@mui/material/Divider';
import Typography from '@mui/material/Typography';
import Tooltip from '@mui/material/Tooltip';
import CircularProgress from '@mui/material/CircularProgress';
import SendIcon from '@mui/icons-material/Send';
import RestartAltIcon from '@mui/icons-material/RestartAlt';
import {
  TICKET_LIMITS,
  createTicketSchema,
  emptyTicket,
  type TicketFormValues,
} from './ticketSchema';
import { demoTickets } from './demoTickets';
import { REQUESTER_ROLES } from '../../api/types';

interface TicketFormProps {
  readonly onSubmit: (values: TicketFormValues) => void;
  readonly isSubmitting: boolean;
  /** Field errors returned by the server, merged into the form so they appear inline. */
  readonly serverErrors?: Record<string, string[]> | null;
}

/**
 * The ticket form. Validated with Zod through React Hook Form, and pre-fillable from the three demo
 * tickets so the dashboard can be exercised in one click.
 *
 * The text inputs carry `dir="auto"`, so a Persian ticket reads right-to-left even while the
 * interface is in English, and an English ticket reads left-to-right while the interface is in
 * Persian. The ticket's language and the interface's language are independent here.
 */
export function TicketForm({ onSubmit, isSubmitting, serverErrors }: TicketFormProps) {
  const { t, i18n } = useTranslation();

  // react-i18next hands back a fresh `t` on every language change, so this rebuilds with it.
  const schema = useMemo(() => createTicketSchema(t), [t]);

  const {
    control,
    handleSubmit,
    reset,
    trigger,
    formState: { errors, isSubmitted },
  } = useForm<TicketFormValues>({
    resolver: zodResolver(schema),
    defaultValues: emptyTicket,
    mode: 'onTouched',
  });

  // Re-run validation after a language change so any visible message is re-rendered in the new
  // language rather than left stranded in the old one.
  useEffect(() => {
    if (isSubmitted) {
      void trigger();
    }
  }, [i18n.language, isSubmitted, trigger]);

  const serverError = (field: keyof TicketFormValues): string | undefined => {
    if (!serverErrors) {
      return undefined;
    }

    // ASP.NET returns PascalCase field names; the form uses camelCase.
    const key = Object.keys(serverErrors).find(
      (name) => name.toLowerCase() === field.toLowerCase(),
    );

    return key ? serverErrors[key]?.[0] : undefined;
  };

  return (
    <Card component="section" aria-labelledby="ticket-form-heading">
      <CardHeader
        id="ticket-form-heading"
        title={t('form.heading')}
        subheader={t('form.subheading')}
        slotProps={{ title: { variant: 'h2' }, subheader: { variant: 'caption' } }}
      />

      <CardContent>
        <Stack
          component="form"
          spacing={2.5}
          noValidate
          onSubmit={(event) => {
            void handleSubmit(onSubmit)(event);
          }}
        >
          <Controller
            name="title"
            control={control}
            render={({ field }) => (
              <TextField
                {...field}
                label={t('form.title')}
                placeholder={t('form.titlePlaceholder')}
                required
                fullWidth
                error={Boolean(errors.title ?? serverError('title'))}
                helperText={errors.title?.message ?? serverError('title') ?? ' '}
                slotProps={{ htmlInput: { maxLength: TICKET_LIMITS.titleMax, dir: 'auto' } }}
              />
            )}
          />

          <Controller
            name="description"
            control={control}
            render={({ field }) => (
              <TextField
                {...field}
                label={t('form.description')}
                placeholder={t('form.descriptionPlaceholder')}
                required
                fullWidth
                multiline
                minRows={6}
                error={Boolean(errors.description ?? serverError('description'))}
                helperText={
                  errors.description?.message ??
                  serverError('description') ??
                  t('form.descriptionHint')
                }
                slotProps={{ htmlInput: { maxLength: TICKET_LIMITS.descriptionMax, dir: 'auto' } }}
              />
            )}
          />

          <Controller
            name="requesterRole"
            control={control}
            render={({ field }) => (
              <TextField
                {...field}
                select
                label={t('form.requesterRole')}
                required
                fullWidth
                error={Boolean(errors.requesterRole ?? serverError('requesterRole'))}
                helperText={errors.requesterRole?.message ?? serverError('requesterRole') ?? ' '}
              >
                {REQUESTER_ROLES.map((role) => (
                  <MenuItem key={role} value={role}>
                    {t(`roles.${role}`)}
                  </MenuItem>
                ))}
              </TextField>
            )}
          />

          <Stack direction="row" spacing={1.5}>
            <Button
              type="submit"
              variant="contained"
              disabled={isSubmitting}
              startIcon={
                isSubmitting ? <CircularProgress size={16} color="inherit" /> : <SendIcon />
              }
              fullWidth
            >
              {isSubmitting ? t('form.submitting') : t('form.submit')}
            </Button>

            <Tooltip title={t('form.reset')} describeChild>
              <span>
                <Button
                  type="button"
                  variant="outlined"
                  color="inherit"
                  disabled={isSubmitting}
                  onClick={() => {
                    reset(emptyTicket);
                  }}
                  aria-label={t('form.reset')}
                >
                  <RestartAltIcon fontSize="small" />
                </Button>
              </span>
            </Tooltip>
          </Stack>

          <Divider flexItem>
            <Typography variant="caption" color="text.secondary">
              {t('form.samplesDivider')}
            </Typography>
          </Divider>

          <Stack spacing={1}>
            {demoTickets.map((demo) => (
              <Tooltip
                key={demo.id}
                title={t(`demos.${demo.id}.hint`)}
                placement="right"
                describeChild
              >
                <Button
                  type="button"
                  variant="outlined"
                  color="secondary"
                  size="small"
                  disabled={isSubmitting}
                  onClick={() => {
                    reset(demo.values);
                  }}
                  sx={{ justifyContent: 'flex-start' }}
                >
                  {t(`demos.${demo.id}.label`)}
                </Button>
              </Tooltip>
            ))}
          </Stack>
        </Stack>
      </CardContent>
    </Card>
  );
}
