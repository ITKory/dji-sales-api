import { cn } from '@/lib/utils';

interface EmptyStateProps {
  icon: React.ReactNode;
  title: string;
  description: string;
  className?: string;
}

export function EmptyState({ icon, title, description, className }: EmptyStateProps) {
  return (
    <div className={cn('flex flex-col items-center justify-center text-center', className)}>
      <div className="relative">
        <div className="absolute inset-0 rounded-full bg-primary/5 blur-xl" />
        <div className="relative flex h-16 w-16 items-center justify-center rounded-2xl bg-gradient-to-br from-muted to-muted/50 ring-1 ring-border/50 animate-empty-float">
          {icon}
        </div>
      </div>
      <p className="mt-5 text-sm font-semibold text-foreground">{title}</p>
      <p className="mt-1.5 max-w-xs text-xs leading-relaxed text-muted-foreground">
        {description}
      </p>
    </div>
  );
}
