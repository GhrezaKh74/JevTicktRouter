import { describe, expect, it } from 'vitest';
import { ticketSchema } from './ticketSchema';
import { demoTickets } from './demoTickets';

describe('ticketSchema', () => {
  it('accepts a well-formed ticket', () => {
    const result = ticketSchema.safeParse({
      title: 'A valid title',
      description: 'A description that is comfortably long enough.',
      requesterRole: 'Customer',
    });

    expect(result.success).toBe(true);
  });

  it.each(demoTickets)('accepts the $id demo ticket', (demo) => {
    expect(ticketSchema.safeParse(demo.values).success).toBe(true);
  });

  it.each([
    ['ab', 'title too short'],
    ['', 'title empty'],
  ])('rejects %s (%s)', (title) => {
    const result = ticketSchema.safeParse({
      title,
      description: 'A description that is comfortably long enough.',
      requesterRole: 'Customer',
    });

    expect(result.success).toBe(false);
  });

  it('rejects a description that is too short', () => {
    const result = ticketSchema.safeParse({
      title: 'A valid title',
      description: 'short',
      requesterRole: 'Customer',
    });

    expect(result.success).toBe(false);
  });

  it('rejects an unknown requester role', () => {
    const result = ticketSchema.safeParse({
      title: 'A valid title',
      description: 'A description that is comfortably long enough.',
      requesterRole: 'ChiefExecutive',
    });

    expect(result.success).toBe(false);
  });

  it('trims surrounding whitespace', () => {
    const result = ticketSchema.parse({
      title: '  A valid title  ',
      description: '  A description that is comfortably long enough.  ',
      requesterRole: 'Customer',
    });

    expect(result.title).toBe('A valid title');
  });

  it('rejects a title that is only whitespace', () => {
    const result = ticketSchema.safeParse({
      title: '     ',
      description: 'A description that is comfortably long enough.',
      requesterRole: 'Customer',
    });

    expect(result.success).toBe(false);
  });
});
