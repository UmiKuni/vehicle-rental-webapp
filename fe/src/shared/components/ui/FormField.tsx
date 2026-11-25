import type { ReactNode } from 'react'
import { cn } from '@/shared/utils'

export function Label({
  className,
  ...props
}: React.LabelHTMLAttributes<HTMLLabelElement>) {
  return (
    <label
      className={cn(
        'text-xs font-bold leading-none peer-disabled:cursor-not-allowed peer-disabled:opacity-70 text-zinc-800 dark:text-zinc-200',
        className
      )}
      {...props}
    />
  )
}

interface FormFieldProps {
  label: ReactNode
  htmlFor: string
  rightAction?: ReactNode
  error?: string
  description?: ReactNode
  required?: boolean
  children: ReactNode
  className?: string
}

export function FormField({
  label,
  htmlFor,
  rightAction,
  error,
  description,
  required,
  children,
  className,
}: FormFieldProps) {
  const descriptionId = description ? `${htmlFor}-description` : undefined
  const errorId = error ? `${htmlFor}-error` : undefined

  return (
    <div className={cn('space-y-2', className)}>
      <div className="flex items-center justify-between gap-2">
        <Label htmlFor={htmlFor} className="inline-flex items-center gap-1">
          <span>{label}</span>
          {required && <span aria-hidden="true" className="text-destructive font-bold text-xs leading-none">*</span>}
          {required && <span className="sr-only">required</span>}
        </Label>
        {rightAction && <div className="text-xs">{rightAction}</div>}
      </div>
      {children}
      {description && <p id={descriptionId} className="mt-1 text-xs leading-5 text-muted-foreground">{description}</p>}
      {error && <p id={errorId} role="alert" className="mt-1 text-xs leading-5 text-destructive">{error}</p>}
    </div>
  )
}
