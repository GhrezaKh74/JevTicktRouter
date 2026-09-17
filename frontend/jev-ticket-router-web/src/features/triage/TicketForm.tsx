import { Controller, useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
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
import { emptyTicket, ticketSchema, type TicketFormValues } from './ticketSchema';
import { demoTickets } from './demoTickets';
import { REQUESTER_ROLES, type RequesterRole } from '../../api/types';

const ROLE_LABELS: Record<RequesterRole, string> = {
  BranchEmployee: 'Branch employee',
  Customer: 'Customer',
  InternalSupport: 'Internal support',
};

interface TicketFormProps {
  readonly onSubmit: (values: TicketFormValues) => void;
  readonly isSubmitting: boolean;
  /** Field errors returned by the server, merged into the form so they appear inline. */
  readonly serverErrors?: Record<string, string[]> | null;
}

/**
 * The ticket form. Validated with Zod through React Hook Form, and pre-fillable from the three demo
 * tickets so the dashboard can be exercised in one click.
 */
export function TicketForm({ onSubmit, isSubmitting, serverErrors }: TicketFormProps) {
  const {
    control,
    handleSubmit,
    reset,
    formState: { errors },
  } = useForm<TicketFormValues>({
    resolver: zodResolver(ticketSchema),
    defaultValues: emptyTicket,
    mode: 'onTouched',
  });

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
        title="Submit a ticket"
        subheader="Persian or English. Nothing you type here leaves your machine in mock mode."
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
                label="Title"
                placeholder="Short summary of the problem"
                required
                fullWidth
                error={Boolean(errors.title ?? serverError('title'))}
                helperText={errors.title?.message ?? serverError('title') ?? ' '}
                slotProps={{ htmlInput: { maxLength: 200, 'aria-describedby': 'title-helper' } }}
              />
            )}
          />

          <Controller
            name="description"
            control={control}
            render={({ field }) => (
              <TextField
                {...field}
                label="Description"
                placeholder="What happened, what you expected, and anything you already tried"
                required
                fullWidth
                multiline
                minRows={6}
                error={Boolean(errors.description ?? serverError('description'))}
                helperText={
                  errors.description?.message ??
                  serverError('description') ??
                  'Avoid pasting real credentials or account numbers.'
                }
                slotProps={{ htmlInput: { maxLength: 5000 } }}
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
                label="Requester role"
                required
                fullWidth
                error={Boolean(errors.requesterRole ?? serverError('requesterRole'))}
                helperText={errors.requesterRole?.message ?? serverError('requesterRole') ?? ' '}
              >
                {REQUESTER_ROLES.map((role) => (
                  <MenuItem key={role} value={role}>
                    {ROLE_LABELS[role]}
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
              {isSubmitting ? 'Analysing…' : 'Triage ticket'}
            </Button>

            <Tooltip title="Clear the form" describeChild>
              <span>
                <Button
                  type="button"
                  variant="outlined"
                  color="inherit"
                  disabled={isSubmitting}
                  onClick={() => {
                    reset(emptyTicket);
                  }}
                  aria-label="Clear the form"
                >
                  <RestartAltIcon fontSize="small" />
                </Button>
              </span>
            </Tooltip>
          </Stack>

          <Divider flexItem>
            <Typography variant="caption" color="text.secondary">
              or try a sample
            </Typography>
          </Divider>

          <Stack spacing={1}>
            {demoTickets.map((demo) => (
              <Tooltip key={demo.id} title={demo.hint} placement="right" describeChild>
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
                  {demo.label}
                </Button>
              </Tooltip>
            ))}
          </Stack>
        </Stack>
      </CardContent>
    </Card>
  );
}
